using System;
using Cysharp.Threading.Tasks;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Sessions;
using Nox.CCK.Utils;
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
				.GetMod("users")
				?.GetInstance<IUserAPI>();

		private static ISessionAPI SessionAPI
			=> _coreAPI.ModAPI
				.GetMod("session")
				?.GetInstance<ISessionAPI>();

		private static IInstanceAPI InstanceAPI
			=> _coreAPI.ModAPI
				.GetMod("instances")
				?.GetInstance<IInstanceAPI>();

		private EventSubscription[] _events = Array.Empty<EventSubscription>();

		public void OnInitializeMain(IMainModCoreAPI api) {
			_coreAPI = api;
			var handlers = new DiscordRpc.EventHandlers {
				disconnectedCallback = OnDisconnected,
				errorCallback        = OnError,
				joinCallback         = OnJoin,
				readyCallback        = OnReady,
				spectateCallback     = OnSpectate,
				requestCallback      = OnRequest
			};

			DiscordRpc.Initialize(ApplicationId, ref handlers, true, null);
			_coreAPI.LoggerAPI.Log("Discord RPC initialized.");
			_isInitialized = true;
			_lastUser      = null;
			_lastInstance  = null;

			_events = new[] {
				api.EventAPI.Subscribe("user_fetch", OnUserUpdateEvent),
				api.EventAPI.Subscribe("instance_fetch", OnInstanceUpdateEvent),
				api.EventAPI.Subscribe("session_current_changed", OnChangeUpdateEvent)
			};
		}

		private void OnUserUpdateEvent(EventData context)
			=> OnUserUpdateEventAsync(context).Forget();

		private void OnInstanceUpdateEvent(EventData context)
			=> OnInstanceUpdateEventAsync(context).Forget();

		private void OnChangeUpdateEvent(EventData context)
			=> OnChangeUpdateEventAsync(context).Forget();

		private IUser _lastUser;
		private IInstance _lastInstance;

		private async UniTask OnUserUpdateEventAsync(EventData context) {
			if (!context.TryGet<IUser>(0, out var user))
				return;
			if (UserAPI?.Current?.Id != user.Id)
				return;
			var session = (SessionAPI != null && SessionAPI.TryGet(SessionAPI.Current, out var s)) ? s : null;
			_lastUser = user;
			await UpdateDetails(_lastUser, _lastInstance, session);
		}

		private async UniTask OnInstanceUpdateEventAsync(EventData context) {
			if (!context.TryGet<IInstance>(0, out var instance))
				return;
			if (SessionAPI == null || !SessionAPI.TryGet(SessionAPI.Current, out var session) || !session.GetInstance().Equals(instance.Identifier))
				return;
			_lastInstance = instance;
			await UpdateDetails(_lastUser, _lastInstance, session);
		}

		private async UniTask OnChangeUpdateEventAsync(EventData context) {
			if (!context.TryGet<ISession>(0, out var session))
				return;
			if (session == null)
				return;
			if (SessionAPI == null || !SessionAPI.TryGet(SessionAPI.Current, out var current) || !current.GetInstance().Equals(session.GetInstance()))
				return;
			_lastInstance = null; // instance will be updated separately if needed
			await UpdateDetails(_lastUser, _lastInstance, current);
		}





		public void OnUpdateMain() {
			if (!_isInitialized)
				return;
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
			var session = (SessionAPI != null && SessionAPI.TryGet(SessionAPI.Current, out var s)) ? s : null;
			UpdateDetails(_lastUser, _lastInstance, session).Forget();
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

		private static string State(ISession session) {
			if (session == null)
				return "In Menu";

			return session.GetTitle()
				?? "In Session";
		}

		private async UniTask UpdateDetails(IUser user, IInstance instance, ISession session) {
			if (!_isReady || !_isInitialized)
				return;


			var thumbnail = user?.Thumbnail ?? "";
			var display   = user?.Display ?? "";

			var presence = new DiscordRpc.RichPresence {
				#if UNITY_EDITOR
				details = "VR Game Development",
				state   = Application.isEditor && !Application.isPlaying ? "In Editor" : State(session),
				#else
				details = "Playing Nox",
				state = State(session),
				#endif
				largeImageKey = string.IsNullOrEmpty(thumbnail)
					? "default"
					: thumbnail,
				largeImageText = string.IsNullOrEmpty(display)
					? "Not logged"
					: display,
				smallImageKey = string.IsNullOrEmpty(thumbnail)
					? ""
					: "default",
				startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				partyMax       = instance?.Capacity ?? 0,
				partySize      = instance?.ClientCount ?? 0,
				partyId        = instance?.Identifier.ToString()
			};

			DiscordRpc.UpdatePresence(presence);
		}

		public void OnDisposeMain() {
			DiscordRpc.Shutdown();
			_isReady       = false;
			_isInitialized = false;
			_lastInstance  = null;
			_lastUser      = null;

			foreach (var sub in _events)
				_coreAPI.EventAPI.Unsubscribe(sub);
			_coreAPI = null;
		}
	}

}