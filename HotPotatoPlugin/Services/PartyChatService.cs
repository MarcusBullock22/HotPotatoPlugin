using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace HotPotatoPlugin.Services;

public sealed class PartyChatService : IDisposable
{
    private const int MaximumMessageLength = 400;

    private readonly Queue<string> messageQueue = new();

    private bool isDisposed;

    public int PendingMessageCount => messageQueue.Count;

    private readonly IPluginLog pluginLog;

    private readonly IFramework framework;

    private DateTime nextMessageTime = DateTime.MinValue;

    private static readonly TimeSpan MessageDelay = TimeSpan.FromMilliseconds(1200);

    public string? NextMessage =>
        messageQueue.Count > 0
            ? messageQueue.Peek()
            : null;


    public PartyChatService(IFramework framework, IPluginLog pluginLog)
    {
        this.framework = framework;
        this.pluginLog = pluginLog;

        this.framework.Update += OnFrameworkUpdate;
    } 

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (isDisposed
            || messageQueue.Count == 0
            || DateTime.UtcNow < nextMessageTime)
        {
            return;
        }

        var message = messageQueue.Peek();

        if (!SendPartyMessage(message))
        {
            return;
        }

        messageQueue.Dequeue();

        nextMessageTime =
            DateTime.UtcNow + MessageDelay;
    }

    public bool SendNextMessage()
    {
        if (isDisposed || messageQueue.Count == 0)
        {
            return false;
        }

        var message = messageQueue.Peek();

        if (!SendPartyMessage(message))
        {
            return false;
        }

        messageQueue.Dequeue();
        return true;
    }

    private unsafe bool SendPartyMessage(string message)
    {
        try
        {
            var uiModule = UIModule.Instance();
            var shellModule = RaptureShellModule.Instance();

            if (uiModule is null || shellModule is null)
            {
                pluginLog.Warning(
                    "The game chat shell was unavailable.");

                return false;
            }

            var command = $"/p {message}";

            using var commandText = new Utf8String(command);

            commandText.SanitizeString(
                AllowedEntities.Unknown9
                | AllowedEntities.Payloads
                | AllowedEntities.OtherCharacters
                | AllowedEntities.SpecialCharacters
                | AllowedEntities.Numbers
                | AllowedEntities.LowercaseLetters
                | AllowedEntities.UppercaseLetters
                | AllowedEntities.CJK);

            if (commandText.Length > 500)
            {
                pluginLog.Warning(
                    "Party command exceeded the allowed length: {Message}",
                    message);

                return false;
            }

            shellModule->ExecuteCommandInner(
                &commandText,
                uiModule);

            pluginLog.Debug(
                "Sent party announcement by user action: {Message}",
                message);

            return true;
        }
        catch (Exception exception)
        {
            pluginLog.Error(
                exception,
                "Failed to send party-chat announcement.");

            return false;
        }
    }

    public void QueueMessage(string message)
    {
        if (isDisposed)
        {
            return;
        }

        var cleanedMessage = message.Trim();

        if (string.IsNullOrWhiteSpace(cleanedMessage))
        {
            return;
        }

        if (cleanedMessage.Length <= MaximumMessageLength)
        {
            messageQueue.Enqueue(cleanedMessage);
            return;
        }

        QueueLongMessage(cleanedMessage);
    }

    public void QueueNumberList(
        string heading,
        IEnumerable<int> numbers)
    {
        if (isDisposed)
        {
            return;
        }

        var numberList = numbers.ToList();

        if (numberList.Count == 0)
        {
            return;
        }

        var currentMessage = heading.Trim();

        foreach (var number in numberList)
        {
            var numberText =
                currentMessage == heading
                    ? $" {number}"
                    : $", {number}";

            if (currentMessage.Length + numberText.Length
                > MaximumMessageLength)
            {
                QueueMessage(currentMessage);
                currentMessage = $"{heading} {number}";
            }
            else
            {
                currentMessage += numberText;
            }
        }

        if (!string.Equals(
            currentMessage,
            heading,
            StringComparison.Ordinal))
        {
            QueueMessage(currentMessage);
        }
    }

    public bool TryTakeNextMessage(out string message)
    {
        if (isDisposed || messageQueue.Count == 0)
        {
            message = string.Empty;
            return false;
        }

        message = messageQueue.Dequeue();
        return true;
    }

    public void SkipNextMessage()
    {
        if (isDisposed || messageQueue.Count == 0)
        {
            return;
        }

        messageQueue.Dequeue();
    }

    public void ClearQueue()
    {
        messageQueue.Clear();
    }

    private void QueueLongMessage(string message)
    {
        var remainingMessage = message;

        while (remainingMessage.Length > MaximumMessageLength)
        {
            var splitPosition = remainingMessage.LastIndexOf(
                ' ',
                MaximumMessageLength);

            if (splitPosition <= 0)
            {
                splitPosition = MaximumMessageLength;
            }

            var section =
                remainingMessage[..splitPosition].Trim();

            if (!string.IsNullOrWhiteSpace(section))
            {
                messageQueue.Enqueue(section);
            }

            remainingMessage =
                remainingMessage[splitPosition..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remainingMessage))
        {
            messageQueue.Enqueue(remainingMessage);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        framework.Update -= OnFrameworkUpdate;
        messageQueue.Clear();
    }
}