using Sandbox;
using Editor;
using TerrorTown;
using System;
using Sandbox.Physics;
using System.Xml.Schema;

namespace end360.TTT
{
    [Library("ttt_grapple_gun")]
    [HammerEntity]
    [Title("Grapple Gun")]
    [Category("Equipment")]
    [TraitorBuyable("Equipment", 1, 1)]
    [DetectiveBuyable("Equipment", 1, 1)]
    [EditorModel("weapons/rust_pistol/rust_pistol.vmdl")]
    public partial class GrappleGun : Carriable, IEquipment
    {
        [ConVar.Replicated("ttt_grapple_force", Help = "Force of the grapple. A small amount goes a long way.")]
        public static float Force { get; set; } = 5;
        [ConVar.Replicated("ttt_grapple_length", Help = "Maximum length of the rope on the grapple gun.")]
        public static float GrappleLength { get; set; } = 2000;
        [ConVar.Replicated("ttt_grapple_counteract_ratio", Help = "How much gravity should be applied.")]
        public static float GravityCounter { get; set; } = 0.4f;
        [ConVar.Replicated("ttt_grapple_speed", Help = "The speed of grapple hook retraction.")]
        public static float PullSpeed { get; set; } = 200;

        public static float Gravity
        {
            get
            {
                if (float.TryParse(ConsoleSystem.GetValue("sv_gravity"), out float grav))
                    return grav;
                return 800;
            }
        }

        public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";
        public override string WorldModelPath => "weapons/rust_pistol/rust_pistol.vmdl";
        
        public ModelEntity? GrappleHook { get; protected set; }
        public float Length { get; set; }
        public Particles? RopeParticles { get; protected set; }
#if DEBUG
        public double Distance { get; set; }
#endif

        TimeSince lastHelpMessage = float.MinValue;
        public override void OnActiveStart()
        {
            base.OnActiveStart();
            if(Game.IsClient && Owner.Client == Game.LocalClient && lastHelpMessage > 15)
            {
                PopupSystem.DisplayPopup(
                    "Left Click - Grapple/Unhook\nRight Click - Shorten\nReload - Lengthen",
                    "Grapple Gun Controls"
                );
                lastHelpMessage = 0;
            }
        }
        public override void OnActiveEnd()
        {
            base.OnActiveEnd();
            Unhook();
        }

        public override void Simulate(IClient cl)
        {
            base.Simulate(cl);

            if (Input.Pressed("Attack1"))
            {
                if (GrappleHook.IsValid())
                    Unhook();
                else
                    ShootGrapple();
            }

            var PullInput = Time.Delta * (Input.Down("Sprint") ? PullSpeed * 2 : PullSpeed);
            if (Input.Down("Attack2"))
                Length = Math.Max(64, Length - PullInput);

            if (Input.Down("Reload"))
                Length = Math.Min(GrappleLength, Length + PullInput);

            PullOwner();
        }

        private void PullOwner()
        {
            if (!GrappleHook.IsValid())
                return;
            
            var distFromMax = MathF.Round(Owner.Position.Distance(GrappleHook.Position) - Length);
#if DEBUG
            Distance = distFromMax;
#endif
            if (distFromMax <= 32)
            {
                if(Owner.GroundEntity == null)
                {
                    Owner.ApplyAbsoluteImpulse(Vector3.Up * Time.Delta * Gravity * GravityCounter);
                }
                return;
            }
            
            var impulse = (GrappleHook.Position - Owner.Position).Normal * Time.Delta * Force * Gravity * GravityCounter;
            Owner.ApplyAbsoluteImpulse(impulse);
        }

        public void Unhook()
        {
            if(RopeParticles != null)
            {
                RopeParticles.Destroy(true);
                RopeParticles = null;
            }

            if(GrappleHook.IsValid())
            {
                GrappleHook.Delete();
                GrappleHook = null;
            }
        }

        private void ShootGrapple()
        {
            if (GrappleHook.IsValid())
                return;
            using (LagCompensation())
            {
                var tr = Trace.Ray(Owner.AimRay, GrappleLength);
                tr.Ignore(this);
                var res = tr.WithoutTags("player", "water", "trigger", "playerclip").Run();
                if (res.Hit)
                {
                    GrappleHook = new ModelEntity("models/citizen_props/hotdog01.vmdl_c")
                    {
                        EnableAllCollisions = false,
                        Position = res.HitPosition,
                        Owner = Owner
#if !DEBUG
                        ,EnableDrawing = false
#endif
                    };
                    GrappleHook.SetupPhysicsFromModel(PhysicsMotionType.Static, true);
                    Length = res.Distance + 64;

                    RopeParticles = Particles.Create("particles/rope.vpcf");
                    ParticleCleanupSystem.RegisterForCleanup(RopeParticles);
                    RopeParticles.SetEntity(0, GrappleHook);
                    RopeParticles.SetEntityAttachment(1, this, "muzzle");

                }
            }
        }
    }
}
