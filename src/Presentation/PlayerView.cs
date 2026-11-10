using Godot;

namespace Game.Presentation;

/// <summary>
/// Renders the player from the assembled character scene (character.tscn) when
/// present, otherwise a capsule. Faces the movement direction and plays idle/walk
/// based on movement. All model-specific wiring (skeleton, animations, skin) lives
/// in the scene, so swapping the character is an asset change, not a code change.
/// Clips are matched by name containing "idle" and "walk"/"run" (case-insensitive).
/// </summary>
public partial class PlayerView : Node3D
{
    private const string ScenePath = "res://assets/models/character/character.tscn";

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
        if (TryLoadCharacter())
        {
            return;
        }
        _visual = CreateCapsule();
        AddChild(_visual);
    }

    private bool TryLoadCharacter()
    {
        if (!ResourceLoader.Exists(ScenePath))
        {
            return false;
        }
        if (GD.Load<PackedScene>(ScenePath) is not PackedScene scene || scene.Instantiate() is not Node3D model)
        {
            return false;
        }

        _visual = model;
        AddChild(_visual);
        _anim = FindAnimationPlayer(_visual);
        ResolveAnimationNames();
        return true;
    }

    private void ResolveAnimationNames()
    {
        if (_anim is null)
        {
            return;
        }
        foreach (string name in _anim.GetAnimationList())
        {
            string clip = ClipName(name);
            if (_idleAnim.Length == 0 && clip.Contains("idle"))
            {
                _idleAnim = name;
            }
            if (_walkAnim.Length == 0 && (clip.Contains("walk") || clip.Contains("run")))
            {
                _walkAnim = name;
            }
        }
    }

    // The leaf clip name, lowercased: strips a "library/" prefix and a "Root|"
    // style prefix so matching ignores the library name (e.g.
    // "character-idle/Root|Idle" -> "idle").
    private static string ClipName(string animationName)
    {
        string tail = animationName;
        int slash = tail.LastIndexOf('/');
        if (slash >= 0)
        {
            tail = tail[(slash + 1)..];
        }
        int bar = tail.LastIndexOf('|');
        if (bar >= 0)
        {
            tail = tail[(bar + 1)..];
        }
        return tail.ToLowerInvariant();
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
