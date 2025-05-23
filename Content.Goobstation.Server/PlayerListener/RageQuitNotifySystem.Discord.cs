// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.CCVar;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Robust.Shared.Network;

namespace Content.Goobstation.Server.PlayerListener;

public sealed partial class RageQuitNotifySystem
{
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    private WebhookData? _webhook;

    private void InitializeDiscord()
    {
        Subs.CVar(_cfg, GoobCVars.PlayerRageQuitDiscordWebhook,
            value =>
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _discord.TryGetWebhook(value, val => _webhook = val);
                }
            },
            true);
    }

    // Inform of a ragequit on discord
    private async void NotifyWebhook(INetChannel channel)
    {
        try
        {
            if (_webhook is null)
                return;

            var hook = _webhook.Value.ToIdentifier();

            var duration = _ticker.RoundDuration();

            // ToString gives us milliseconds, and we don't really need it.
            var time =
                $"{duration.Hours}:{duration.Minutes}:{duration.Seconds}";

            var message = Loc.GetString("rage-quit-notify-discord",
                ("round", _ticker.RoundId),
                ("time", time),
                ("player", channel.UserName));

            var payload = new WebhookPayload
            {
                Content = message,
            };

            await _discord.CreateMessage(hook, payload);
        }
        // Doing `async void` without catching exceptions is LE BAD, okay?
        catch (Exception e)
        {
            Log.Error($"Failed to send ragequit information to webhook!\n{e}");
        }
    }
}