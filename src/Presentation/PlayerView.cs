using Godot;

namespace Game.Presentation;

/// <summary>
/// Renders the player. Prefers a pre-assembled character.tscn; otherwise assembles
/// the character in code from the model FBX plus separate animation FBX files,
/// merging their idle/run clips onto one AnimationPlayer; otherwise a capsule.
/// Faces the movement direction and plays idle/walk based on movement.
/// </summary>
public partial class PlayerView : Node3D
{
    private const string ScenePath = "res://assets/models/character/character.tscn";
    private const string ModelPath = "res://assets/models/character/character.fbx";
    private const string IdlePath = "res://assets/models/character/character-idle.fbx";
    private const string RunPath = "res://assets/models/character/character-run.fbx";

    private Node3D _visual = null!;
    private AnimationPlayer? _anim;
    private string _idleAnim = "";
    private string _walkAnim = "";
    private float _yaw;

    public override void _Ready()
    {
        Build();
    }

    public void UpdateFrom(Vector3 position, Vector2 moveDir, double delta)
    {
        Position = position;

        bool moving = moveDir.LengthSquared() > 0.0001f;
        if (moving)
        {
            float target = Mathf.Atan2(moveDir.X, moveDir.Y);
            float weight = (float)System.Math.Min(1.0, 12.0 * delta);
            _yaw = Mathf.LerpAngle(_yaw, target, weight);
            _visual.Rotation = new Vector3(0f, _yaw, 0f);
        }

        PlayAnimation(moving ? _walkAnim : _idleAnim);
    }

    private void PlayAnimation(string name)
    {
        if (_anim is null || name.Length == 0 || _anim.CurrentAnimation == name)
        {
            return;
        }
        _anim.Play(name);
    }

    private void Build()
    {
        if (TryBuildFromScene() || TryBuildFromParts())
        {
            return;
        }
        _visual = CreateCapsule();
        AddChild(_visual);
    }

    private bool TryBuildFromScene()
    {
        if (!TryInstance(ScenePath, out Node3D? scene))
        {
            return false;
        }
        _visual = scene!;
        AddChild(_visual);
        _anim = FindAnimationPlayer(_visual);
        ResolveAnimationNames();
        return true;
    }

    private bool TryBuildFromParts()
    {
        if (!TryInstance(ModelPath, out Node3D? model))
        {
            return false;
        }
        _visual = model!;
        AddChild(_visual);

        var player = new AnimationPlayer
        {
            Name = "AnimationPlayer",
            RootNode = ".."
        };
        _visual.AddChild(player);
        _anim = player;

        MergeClip(IdlePath, "idle", "idle", player);
        MergeClip(RunPath, "run", "run", player);
        ResolveAnimationNames();
        return true;
    }

    private static void MergeClip(string path, string wantSubstring, string targetName, AnimationPlayer target)
    {
        if (!TryInstance(path, out Node3D? root) || root is null)
        {
            return;
        }

        AnimationPlayer? source = FindAnimationPlayer(root);
        if (source is not null)
        {
            foreach (string name in source.GetAnimationList())
            {
                if (!name.ToLowerInvariant().Contains(wantSubstring))
                {
                    continue;
                }

                StringName defaultLibrary = "";
                if (!target.HasAnimationLibrary(defaultLibrary))
                {
                    target.AddAnimationLibrary(defaultLibrary, new AnimationLibrary());
                }
                target.GetAnimationLibrary(defaultLibrary).AddAnimation(targetName, source.GetAnimation(name));
                break;
            }
        }

        root.QueueFree();
    }

    private void ResolveAnimationNames()
    {
        if (_anim is null)
        {
            return;
        }
        foreach (string name in _anim.GetAnimationList())
        {
            string lower = name.ToLowerInvariant();
            if (_idleAnim.Length == 0 && lower.Contains("idle"))
            {
                _idleAnim = name;
            }
            if (_walkAnim.Length == 0 && (lower.Contains("walk") || lower.Contains("run")))
            {
                _walkAnim = name;
            }
        }
    }

    private static bool TryInstance(string path, out Node3D? node)
    {
        node = null;
        if (!ResourceLoader.Exists(path))
        {
            return false;
        }
        if (GD.Load<PackedScene>(path) is not PackedScene scene || scene.Instantiate() is not Node3D instanced)
        {
            return false;
        }
        node = instanced;
        return true;
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player)
        {
            return player;
        }
        foreach (Node child in node.GetChildren())
        {
            AnimationPlayer? found = FindAnimationPlayer(child);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static MeshInstance3D CreateCapsule()
    {
        return new MeshInstance3D
        {
            Name = "Capsule",
            Mesh = new CapsuleMesh
            {
                Radius = 0.35f,
                Height = 1.4f
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.90f, 0.55f, 0.20f)
            },
            Position = new Vector3(0f, 0.7f, 0f)
        };
    }
}
