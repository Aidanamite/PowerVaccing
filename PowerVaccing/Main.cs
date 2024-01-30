using HarmonyLib;
using SRML;
using SRML.Console;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Console = SRML.Console.Console;
using Object = UnityEngine.Object;

namespace PowerVaccing
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";

        public override void PreLoad()
        {
            HarmonyInstance.PatchAll();
        }
        public static void Log(string message) => Console.Log($"[{modName}]: " + message);
        public static void LogError(string message) => Console.LogError($"[{modName}]: " + message);
        public static void LogWarning(string message) => Console.LogWarning($"[{modName}]: " + message);
        public static void LogSuccess(string message) => Console.LogSuccess($"[{modName}]: " + message);
    }

    [HarmonyPatch(typeof(WeaponVacuum), "AirBurst")]
    class Patch_PulseWave
    {
        static bool Prefix(WeaponVacuum __instance)
        {
            if (__instance.vacMode != WeaponVacuum.VacMode.VAC)
                return true;
            if (__instance.player.GetCurrEnergy() < __instance.staminaPerBurst)
            {
                __instance.PlayTransientAudio(__instance.vacBurstNoEnergyCue);
                return false;
            }
            __instance.player.SpendEnergy(__instance.staminaPerBurst);
            float dist = float.PositiveInfinity;
            GameObject nearest = null;
            Vector3 playPos = __instance.player.transform.position;
            foreach (var i in __instance.tracker.CurrColliders())
            {
                var vacu = i.GetComponent<Vacuumable>();
                var ident = i.GetComponent<Identifiable>();
                var cycle = i.GetComponent<ResourceCycle>();
                if (vacu && ident && ident.id != Identifiable.Id.NONE && vacu.enabled && vacu.size == Vacuumable.Size.NORMAL && (!cycle || cycle.GetState() == ResourceCycle.State.EDIBLE || cycle.GetState() == ResourceCycle.State.RIPE))
                    __instance.StartCoroutine(__instance.StartConsuming(vacu, ident.id));
                else if (vacu && ident && ident.id != Identifiable.Id.NONE && vacu.enabled && vacu.size == Vacuumable.Size.LARGE)
                {
                    var d = (vacu.transform.position - playPos).sqrMagnitude;
                    if (d < dist)
                    {
                        nearest = i.gameObject;
                        dist = d;
                    }
                }
            }
            if (nearest)
            {
                var vacu = nearest.GetComponent<Vacuumable>();
                var ident = nearest.GetComponent<Identifiable>();
                __instance.held = nearest;
                __instance.SetHeldRad(PhysicsUtil.RadiusOfObject(SRSingleton<GameContext>.Instance.LookupDirector.GetPrefab(ident.id)));
                vacu.hold();
                if (Identifiable.IsLargo(ident.id))
                {
                    SceneContext.Instance.TutorialDirector.MaybeShowPopup(TutorialDirector.Id.LARGO);
                    SceneContext.Instance.PediaDirector.MaybeShowPopup(PediaDirector.Id.LARGO_SLIME);
                }
                var feral = nearest.GetComponent<SlimeFeral>();
                if (feral && feral.IsFeral())
                    SceneContext.Instance.PediaDirector.MaybeShowPopup(PediaDirector.Id.FERAL_SLIME);
                __instance.heldStartTime = SceneContext.Instance.TimeDirector.WorldTime();
                __instance.held.GetComponent<SlimeEat>()?.ResetEatClock();
                __instance.held.GetComponent<TentacleGrapple>()?.Release();
                __instance.held.GetComponent<GroundVine>()?.Release();
                SceneContext.Instance.PediaDirector.MaybeShowPopup(ident.id);
                __instance.PlayTransientAudio(__instance.vacHeldCue);
                SRSingleton<SceneContext>.Instance.Player.GetComponent<ScreenShaker>().ShakeFrontImpact(1f);
            }
            return false;
        }
    }
}