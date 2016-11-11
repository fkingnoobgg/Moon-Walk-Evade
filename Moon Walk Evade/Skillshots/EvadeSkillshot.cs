using System;
using EloBuddy;
using EloBuddy.SDK;
using SharpDX;

namespace Moon_Walk_Evade.Skillshots
{
    public abstract class EvadeSkillshot
    {
        public SpellDetector SpellDetector { get; set; }
        public GameObject SpawnObject { get; set; }
        public Obj_AI_Base Caster { get; set; }
        public GameObjectProcessSpellCastEventArgs CastArgs { get; set; }
        public EloBuddy.SpellData SData { get; set; }
        public SpellData OwnSpellData { get; set; }
        public GameObjectTeam Team { get; set; }
        public bool IsActive { get; set; }
        public bool IsValid { get; set; }
        public bool CastComplete { get; set; }
        public int TimeDetected { get; set; }

        public bool IsProcessSpellCast => Caster != null;

        public string DisplayText => $"{OwnSpellData.ChampionName} {OwnSpellData.Slot} - {OwnSpellData.DisplayName}";

        public abstract Vector3 FixedStartPosition { get; set; }

        public abstract Vector3 FixedEndPosition { get; set; }

        public abstract Vector3 GetCurrentPosition();

        public abstract Vector2 GetMissilePosition(int extraTime);

        /// <summary>
        /// with missile
        /// </summary>
        public abstract void OnCreateObject(GameObject obj);

        /// <summary>
        /// with missile
        /// </summary>
        public virtual void OnDeleteObject(GameObject obj) { }

        /// <summary>
        /// obj == null ? procspellcast / no missile : objectcreate / missile
        /// </summary>
        public virtual void OnCreateUnsafe(GameObject obj) { }

        public virtual bool OnDeleteMissile(GameObject obj)
        {
            if (OwnSpellData.ExtraExistingTime == 0)
                return true;
            else Core.DelayAction(() => IsValid = false, TimeDetected + OwnSpellData.Delay + OwnSpellData.ExtraExistingTime - Environment.TickCount);

            return false;
        }

        public virtual void OnDispose() { }

        /// <summary>
        /// real danger area
        /// </summary>
        public abstract void OnDraw();

        public abstract void OnTick();

        /// <summary>
        /// without missile
        /// </summary>
        /// <param name="sender"></param>
        public virtual void OnSpellDetection(Obj_AI_Base sender) { }

        public abstract Geometry.Polygon ToPolygon();

        /// <summary>
        /// For Veigar E
        /// </summary>
        public virtual Geometry.Polygon ToInnerPolygon()
        {
            return new Geometry.Polygon();
        }

        /// <summary>
        /// For Veigar E
        /// </summary>
        public virtual Geometry.Polygon ToOuterPolygon()
        {
            return new Geometry.Polygon();
        }

        public abstract int GetAvailableTime(Vector2 pos);

        public abstract bool IsFromFow();

        public abstract EvadeSkillshot NewInstance(bool debug = false);

        public override string ToString()
        {
            return $"{OwnSpellData.ChampionName}_{OwnSpellData.Slot}_{OwnSpellData.DisplayName}";
        }

        public abstract bool IsSafe(Vector2? p = null);
    }
}