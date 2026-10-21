using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Relicbound.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace Relicbound.Unity
{
    public sealed class ArenaClient : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> _actors = new Dictionary<string, GameObject>();
        private readonly HashSet<UnityWebRequest> _requests = new HashSet<UnityWebRequest>();
        private string _server = "http://127.0.0.1:5188";
        private SessionReply _session;
        private WorldSnapshot _world;
        private string _target = "skeleton-a";
        private string _message = "Start the .NET server, then connect.";
        private long _sequence;
        private bool _busy;
        private Camera _camera;
        private Material _material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ArenaClient>() == null)
            {
                new GameObject("RELICBOUND Client").AddComponent<ArenaClient>();
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;
            _camera = Camera.main;
            if (_camera == null)
            {
                _camera = new GameObject("Isometric Camera").AddComponent<Camera>();
            }
            _camera.transform.position = new Vector3(14, 20, -14);
            _camera.transform.LookAt(Vector3.zero);
            _camera.orthographic = true;
            _camera.orthographicSize = 15;
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena";
            floor.transform.position = new Vector3(0, -0.2f, 0);
            floor.transform.localScale = new Vector3(22, 0.2f, 22);
            floor.GetComponent<Renderer>().sharedMaterial = _material;
            var shrine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shrine.name = "Runic Shrine";
            shrine.transform.position = new Vector3(-4, 0.5f, -3);
            Paint(shrine, Color.cyan);
            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
        }

        private IEnumerator Connect()
        {
            _busy = true;
            using (var request = Request("/api/sessions", "POST"))
            {
                yield return request.SendWebRequest();
                _requests.Remove(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _message = request.error;
                    _busy = false;
                    yield break;
                }
                _session = JsonUtility.FromJson<SessionReply>(request.downloadHandler.text);
            }
            _sequence = 0;
            _busy = false;
            _message = "Connected. Discover Lightning Infusion at the cyan shrine.";
            StartCoroutine(Poll());
        }

        private IEnumerator Poll()
        {
            var session = _session;
            while (session != null && ReferenceEquals(_session, session))
            {
                using (var request = Request("/api/world", "GET"))
                {
                    yield return request.SendWebRequest();
                    _requests.Remove(request);
                    if (!ReferenceEquals(_session, session)) yield break;
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        _world = JsonUtility.FromJson<WorldSnapshot>(request.downloadHandler.text);
                        RenderWorld();
                    }
                    else
                    {
                        _message = request.error;
                        if (request.responseCode == 401) _session = null;
                    }
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        private void Send(WorldCommand command)
        {
            if (_session == null || _busy) return;
            command.Sequence = ++_sequence;
            StartCoroutine(SendCommand(command));
        }

        private IEnumerator SendCommand(WorldCommand command)
        {
            _busy = true;
            using (var request = Request("/api/commands", "POST", JsonUtility.ToJson(command)))
            {
                yield return request.SendWebRequest();
                _requests.Remove(request);
                _message = request.result == UnityWebRequest.Result.Success
                    ? JsonUtility.FromJson<CommandReply>(request.downloadHandler.text).Message
                    : request.error;
            }
            _busy = false;
        }

        private UnityWebRequest Request(string path, string method, string body = null)
        {
            var request = new UnityWebRequest(_server.TrimEnd('/') + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 5;
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (_session != null) request.SetRequestHeader("Authorization", "Bearer " + _session.Token);
            _requests.Add(request);
            return request;
        }

        private void RenderWorld()
        {
            var visible = new HashSet<string>();
            foreach (var actor in _world.Actors)
            {
                visible.Add(actor.Id);
                if (!_actors.TryGetValue(actor.Id, out var view))
                {
                    view = GameObject.CreatePrimitive(actor.IsPlayer ? PrimitiveType.Capsule : PrimitiveType.Cube);
                    view.name = actor.Id;
                    _actors.Add(actor.Id, view);
                }
                view.SetActive(actor.Health > 0);
                view.transform.position = new Vector3(actor.X, 0.6f, actor.Z);
                Paint(view, actor.Frozen ? Color.cyan : actor.IsPlayer
                    ? (actor.Id == _session.PlayerId ? Color.green : Color.blue) : Color.red);
            }
            foreach (var id in _actors.Keys.Where(id => !visible.Contains(id)).ToArray())
            {
                Destroy(_actors[id]);
                _actors.Remove(id);
            }
        }

        private void Paint(GameObject view, Color color)
        {
            var renderer = view.GetComponent<Renderer>();
            renderer.sharedMaterial = _material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        // Prototype Controls: IMGUI works without depending on either Unity input package.
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 350, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("RELICBOUND — Cooperative Arena");
            if (_session == null)
            {
                _server = GUILayout.TextField(_server);
                GUI.enabled = !_busy;
                if (GUILayout.Button("Connect")) StartCoroutine(Connect());
            }
            else
            {
                var player = _world?.Actors.FirstOrDefault(actor => actor.Id == _session.PlayerId);
                GUILayout.Label(player == null ? "Loading..." : $"Health {player.Health}/{player.MaxHealth} | Shield {player.Barrier} | Resource {player.Resource}");
                GUILayout.Label($"Tick {_world?.Tick} | Branch {_world?.Branch}");
                GUI.enabled = !_busy;
                MoveButton("North", 0, 1);
                GUILayout.BeginHorizontal();
                MoveButton("West", -1, 0);
                MoveButton("South", 0, -1);
                MoveButton("East", 1, 0);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Dodge north")) Send(new WorldCommand { Kind = "dodge", Z = 1 });
                if (GUILayout.Button("Interact with shrine")) Send(new WorldCommand { Kind = "interact" });
                if (GUILayout.Button("Equip Lightning Infusion")) Loadout("");
                if (GUILayout.Button("Select Tempest Mastery")) Loadout("tempest");
                if (GUILayout.Button("Select Storm Guardian Mastery")) Loadout("storm-guardian");
                if (GUILayout.Button("Branch: Ruin")) Send(new WorldCommand { Kind = "branch", TargetId = "ruin" });
                if (GUILayout.Button("Branch: Silence")) Send(new WorldCommand { Kind = "branch", TargetId = "silence" });
                foreach (var enemy in _world?.Actors.Where(actor => !actor.IsPlayer && actor.Health > 0) ?? Enumerable.Empty<ActorSnapshot>())
                {
                    if (GUILayout.Button($"{(_target == enemy.Id ? "> " : "")}{enemy.Id}: {enemy.Health} HP")) _target = enemy.Id;
                }
                if (GUILayout.Button("Freeze selected enemy")) Send(new WorldCommand { Kind = "freeze", TargetId = _target });
                if (GUILayout.Button("Crushing Blow")) Send(new WorldCommand { Kind = "attack", TargetId = _target,
                    AllyId = _world?.Actors.FirstOrDefault(actor => actor.IsPlayer && actor.Id != _session.PlayerId && actor.Health > 0 && player != null &&
                        Mathf.Abs(actor.X - player.X) + Mathf.Abs(actor.Z - player.Z) <= 4)?.Id ?? "" });
                if (GUILayout.Button("Disconnect")) StartCoroutine(Disconnect());
            }
            GUI.enabled = true;
            GUILayout.Label(_message);
            GUILayout.EndArea();
        }

        private void MoveButton(string label, int x, int z)
        {
            if (GUILayout.Button(label)) Send(new WorldCommand { Kind = "move", X = x, Z = z });
        }

        private void Loadout(string mastery) => Send(new WorldCommand
        {
            Kind = "loadout", DiscoveryId = "lightning-infusion", MasteryId = mastery
        });

        private IEnumerator Disconnect()
        {
            _busy = true;
            using (var request = Request("/api/session", "DELETE"))
            {
                yield return request.SendWebRequest();
                _requests.Remove(request);
            }
            _session = null;
            _busy = false;
            _message = "Disconnected. Restart the server to reset the expedition.";
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var request in _requests)
            {
                request.Abort();
                request.Dispose();
            }
            _requests.Clear();
            foreach (var view in _actors.Values) Destroy(view);
            if (_material != null) Destroy(_material);
        }
    }
}
