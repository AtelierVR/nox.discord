using System;
using Cysharp.Threading.Tasks;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Sessions;
using Nox.Instances;
using Nox.Sessions;
using Nox.Users;
using UnityEngine;

namespace Nox.Discord {

    public class Main : IMainModInitializer {
        private static IMainModCoreAPI _coreAPI;

        private const string ApplicationId = "1353926096487190618";
        private bool _isInitialized;
        private bool _isReady;

        private static IUserAPI UserAPI
            => _coreAPI.ModAPI
                       .GetMod("user")
                       ?.GetInstance<IUserAPI>();

        private static ISessionAPI SessionAPI
            => _coreAPI.ModAPI
                       .GetMod("session")
                       ?.GetInstance<ISessionAPI>();

        private static IInstanceAPI InstanceAPI
            => _coreAPI.ModAPI
                       .GetMod("instance")
                       ?.GetInstance<IInstanceAPI>();

        private EventSubscription[] _events = Array.Empty<EventSubscription>();

        public void OnInitializeMain(IMainModCoreAPI api) {
            _coreAPI = api;
            var handlers = new DiscordRpc.EventHandlers {
                disconnectedCallback = OnDisconnected, errorCallback = OnError, joinCallback = OnJoin, readyCallback = OnReady,
                spectateCallback = OnSpectate, requestCallback = OnRequest
            };

            DiscordRpc.Initialize(ApplicationId, ref handlers, true, null);
            _coreAPI.LoggerAPI.Log("Discord RPC initialized.");
            _isInitialized = true;

            _events = new[] { api.EventAPI.Subscribe("user_fetch", OnUpdateEvent), api.EventAPI.Subscribe("instance_fetch", OnUpdateEvent), api.EventAPI.Subscribe("session_current_changed", OnUpdateEvent) };
        }

        public void OnUpdateMain() {
            if (!_isInitialized) return;
            DiscordRpc.RunCallbacks();
        }

        private string ToDisplay(ref DiscordRpc.DiscordUser user)
            => $"{user.username}{(user.discriminator == "0" ? "" : $"#{user.discriminator}")}";

        private void OnRequest(ref DiscordRpc.DiscordUser user) {
            _coreAPI.LoggerAPI.LogDebug($"Friend request from {ToDisplay(ref user)} ({user.userId})");
        }

        private void OnSpectate(string secret) {
            _coreAPI.LoggerAPI.LogDebug($"Spectate request from {secret}");
        }

        private void OnReady(ref DiscordRpc.DiscordUser user) {
            _coreAPI.LoggerAPI.LogDebug($"Connected to Discord as {ToDisplay(ref user)} ({user.userId})");
            _isReady = true;
            UpdateDetails().Forget();
        }

        private void OnJoin(string secret) {
            _coreAPI.LoggerAPI.LogDebug($"Join request from {secret}");
        }

        private void OnError(int code, string message) {
            _coreAPI.LoggerAPI.LogError($"Discord RPC Error {code}: {message}");
        }

        private void OnDisconnected(int code, string message) {
            _coreAPI.LoggerAPI.LogWarning($"Disconnected from Discord RPC {code}: {message}");
            _isReady = false;
        }

        private void OnUpdateEvent(EventData context)
            => UpdateDetails().Forget();

        private static string State(ISession session) {
            if (session == null) return "In Menu";

            return session.GetTitle()
                ?? "In Session";
        }

        private async UniTask UpdateDetails() {
            if (!_isReady || !_isInitialized) return;

            var user = UserAPI?.GetCurrent();
            var session = SessionAPI != null && SessionAPI.TryGet(SessionAPI.Current, out var s) ? s : null;


            var instanceId = session?.GetInstance();

            IInstance instance = null;
            if (instanceId != null && instanceId.IsValid() && InstanceAPI != null) 
                instance = await InstanceAPI.Fetch(instanceId);

            _coreAPI.LoggerAPI.LogDebug($"Updating Discord presence for user '{user?.GetDisplay() ?? "Not logged"}' in session '{session?.GetTitle() ?? "No session"}'");

            var thumbnail = user != null ? user.GetThumbnailUrl() : "";
            var display = user != null ? user.GetDisplay() : "";

            var presence = new DiscordRpc.RichPresence {
                #if UNITY_EDITOR
                details = "VR Game Development", state = Application.isEditor && !Application.isPlaying ? "In Editor" : State(session),
                #else
				details = "Playing Nox",
				state = State(session),
                #endif
                largeImageKey = string.IsNullOrEmpty(thumbnail) ? "default" : thumbnail, largeImageText = string.IsNullOrEmpty(display) ? "Not logged" : display, smallImageKey = string.IsNullOrEmpty(thumbnail) ? "" : "default", startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                partyMax = instance?.GetCapacity() ?? 0, partySize = instance?.GetPlayerCount() ?? 0, partyId = instance?.ToIdentifier()?.ToString()
            };

            DiscordRpc.UpdatePresence(presence);
        }

        public void OnDisposeMain() {
            DiscordRpc.Shutdown();
            _isReady = false;
            _isInitialized = false;

            foreach (var sub in _events) _coreAPI.EventAPI.Unsubscribe(sub);
            _coreAPI = null;
        }
    }

}