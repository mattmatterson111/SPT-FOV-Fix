﻿using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.InputSystem;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static EFT.Player;
using FCSubClass = EFT.Player.FirearmController.GClass1595;
using InputClass = Class1477;
using SightComptInterface = GInterface318;

namespace FOVFix
{

    public class OpticPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(OpticCratePanel).GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(OpticCratePanel __instance)
        {
            Plugin.FovController.OpticPanel = __instance;   
        }
    }

    public class KeyInputPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InputClass).GetMethod("TranslateCommand", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool PatchPrefix(InputClass __instance, ECommand command)
        {
            if (Plugin.AllowToggleMag.Value && command == ECommand.ChangeScopeMagnification && Plugin.EnableVariableZoom.Value && !Plugin.FovController.IsFixedMag && Plugin.FovController.IsOptic && (!Plugin.FovController.CanToggle || Plugin.FovController.CanToggleButNotFixed) && (Plugin.FovController.IsAiming || Plugin.ToggleMagOutsideADS.Value))
            {
                Plugin.FovController.ToggledMagnification = !Plugin.FovController.ToggledMagnification;
                float zoom = 
                    Plugin.FovController.CurrentZoom >= Plugin.FovController.MinZoom && Plugin.FovController.CurrentZoom <= Plugin.FovController.MaxZoom / 2 ? Plugin.FovController.MaxZoom :
                    Plugin.FovController.CurrentZoom <= Plugin.FovController.MaxZoom && Plugin.FovController.CurrentZoom > Plugin.FovController.MaxZoom / 2 ? Plugin.FovController.MinZoom :
                    Plugin.FovController.CurrentZoom;
                Plugin.FovController.HandleZoomInput(zoom, true);
            }

            return true;
        }
    }

    public class CalculateScaleValueByFovPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod("CalculateScaleValueByFov");
        }

        [PatchPrefix]
        public static bool Prefix(ref float ____ribcageScaleCompensated)
        {
            ____ribcageScaleCompensated = Plugin.FovScale.Value;
            return false;
        }
    }

    public class OperationSetScopeModePatch : ModulePatch
    {
        private static FieldInfo fAnimatorField;
        private static FieldInfo weaponStateField;

        protected override MethodBase GetTargetMethod()
        {
            fAnimatorField = AccessTools.Field(typeof(FCSubClass), "firearmsAnimator_0");
            weaponStateField = AccessTools.Field(typeof(FCSubClass), "weaponManagerClass");

            return typeof(FCSubClass).GetMethod("SetScopeMode", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool PatchPrefix(FCSubClass __instance, FirearmScopeStateStruct[] scopeStates)
        {
            if (__instance.CanChangeScopeStates(scopeStates))
            {
                if (Plugin.FovController.CanToggle || !Plugin.FovController.IsOptic)
                {
                    FirearmsAnimator fAnimator = (FirearmsAnimator)fAnimatorField.GetValue(__instance);
                    fAnimator.ModToggleTrigger();
                }
                WeaponManagerClass weaponState = (WeaponManagerClass)weaponStateField.GetValue(__instance);
                weaponState.UpdateScopesMode();
            }
            return false;
        }
    }

    public class ChangeAimingModePatch : ModulePatch
    {
        private static FieldInfo playerField;

        protected override MethodBase GetTargetMethod()
        {
            playerField = AccessTools.Field(typeof(EFT.Player.FirearmController), "_player");
            return typeof(Player.FirearmController).GetMethod("ChangeAimingMode", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static void PatchPrefix(Player.FirearmController __instance)
        {
            Plugin.FovController.ChangeSight = true;
        }
    }

    public class SetScopeModePatch : ModulePatch
    {
        private static FieldInfo playerField;
        private static FieldInfo sighCompField;

        private static bool canToggle = false;
        private static bool isFixedMag = false;
        private static bool isOptic = false; 
/*        private static bool isCurrentlyFucky = false;*/
        private static bool canToggleButNotFixed = false;

        protected override MethodBase GetTargetMethod()
        {
            playerField = AccessTools.Field(typeof(EFT.Player.FirearmController), "_player");
            sighCompField = AccessTools.Field(typeof(EFT.InventoryLogic.SightComponent), "_template");

            return typeof(Player.FirearmController).GetMethod("SetScopeMode", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool PatchPrefix(Player.FirearmController __instance)
        {
            Player player = (Player)playerField.GetValue(__instance);
            ProceduralWeaponAnimation pwa = player.ProceduralWeaponAnimation;
            Mod currentAimingMod = (player.ProceduralWeaponAnimation.CurrentAimingMod != null) ? player.ProceduralWeaponAnimation.CurrentAimingMod.Item as Mod : null;

            if (Plugin.FovController.IsOptic || currentAimingMod.TemplateId == "5c07dd120db834001c39092d" || currentAimingMod.TemplateId == "5c0a2cec0db834001b7ce47d") 
            {
                Plugin.FovController.ChangeSight = true;


                isOptic = pwa.CurrentScope.IsOptic;
                SightComponent sightComp = player.ProceduralWeaponAnimation.CurrentAimingMod;
                SightModClass sightModClass = currentAimingMod as SightModClass;
                SightComptInterface zooms = (SightComptInterface)sighCompField.GetValue(sightModClass.Sight);

                canToggle = currentAimingMod.Template.ToolModdable;
                isFixedMag = currentAimingMod.Template.HasShoulderContact;
                canToggleButNotFixed = canToggle && !isFixedMag;

                float minZoom = 1f;
                float maxZoom = 1f;

                if (isFixedMag)
                {
                    minZoom = zooms.Zooms[0][0];
                    maxZoom = minZoom;
                }
                else if (canToggleButNotFixed && zooms.Zooms[0].Length > 2)
                {
                    minZoom = zooms.Zooms[0][0];
                    maxZoom = zooms.Zooms[0][2];
                }
                else
                {
                    minZoom = zooms.Zooms[0][0];
                    maxZoom = zooms.Zooms[0][1];
                }

                /*                isCurrentlyFucky = (minZoom < 2 && sightComp.SelectedScopeIndex == 0 && sightComp.SelectedScopeMode == 0 && !isFixedMag && !canToggle);
                */

                bool isSamVudu = currentAimingMod.TemplateId == "5b3b99475acfc432ff4dcbee" && Plugin.SamSwatVudu.Value;
                if ((!canToggle && !Plugin.FovController.IsFucky && !isSamVudu) || (isSamVudu && !Plugin.FovController.DidToggleForFirstPlane))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        [PatchPostfix]
        private static void PatchPostfix(Player.FirearmController __instance)
        {
            Player player = (Player)playerField.GetValue(__instance);
            ProceduralWeaponAnimation pwa = player.ProceduralWeaponAnimation;
            Mod currentAimingMod = (player.ProceduralWeaponAnimation.CurrentAimingMod != null) ? player.ProceduralWeaponAnimation.CurrentAimingMod.Item as Mod : null;
            if (isOptic || currentAimingMod.TemplateId == "5c07dd120db834001c39092d" || currentAimingMod.TemplateId == "5c0a2cec0db834001b7ce47d")
            {
                if (!canToggle)
                {
                    return;
                }

                if (isFixedMag)
                {
                    float currentToggle = player.ProceduralWeaponAnimation.CurrentAimingMod.GetCurrentOpticZoom();
                    Plugin.FovController.CurrentZoom = currentToggle;
                    Plugin.FovController.ZoomScope(currentToggle);
                }
            }
        }
    }



    public class IsAimingPatch : ModulePatch
    {
        private static FieldInfo playerField;
        private static FieldInfo sightComptField;

        private static bool hasSetFov = false;
        private static float adsTimer = 0f;

        protected override MethodBase GetTargetMethod()
        {
            playerField = AccessTools.Field(typeof(EFT.Player.FirearmController), "_player");
            sightComptField = AccessTools.Field(typeof(EFT.InventoryLogic.SightComponent), "_template");

            return typeof(Player.FirearmController).GetMethod("get_IsAiming", BindingFlags.Instance | BindingFlags.Public);
        }

        private static Item getContainedItem (Slot x)
        {
            return x.ContainedItem;
        }

        private static string getSightComp(SightComponent x)
        {
            return x.Item.Name;
        }

        private static bool hasScopeAimBone(SightComponent sightComp, Player player)
        {
            List<ProceduralWeaponAnimation.SightNBone> scopeAimTransforms = player.ProceduralWeaponAnimation.ScopeAimTransforms;
            for (int i = 0; i < scopeAimTransforms.Count; i++)
            {
                if (scopeAimTransforms[i].Mod != null && scopeAimTransforms[i].Mod.Equals(sightComp))
                {
                    return true;
                }
            }
            return false;
        }

        private static FirearmScopeStateStruct[] getScopeModeFullList(Weapon weapon, Player player)
        {
            //you can thank BSG for this monstrosity 
            IEnumerable<SightComponent> sightEnumerable = Enumerable.OrderBy<SightComponent, string>(Enumerable.Select<Slot, Item>(weapon.AllSlots, new Func<Slot, Item>(getContainedItem)).GetComponents<SightComponent>(), new Func<SightComponent, string>(getSightComp));
            List<FirearmScopeStateStruct> sightStructList = new List<FirearmScopeStateStruct>();
            int aimIndex = weapon.AimIndex.Value;
            int index = 0;
            foreach (SightComponent sightComponent in sightEnumerable) 
            {
                if (hasScopeAimBone(sightComponent, player)) 
                {
                    for (int i = 0; i < sightComponent.ScopesCount; i++)
                    {
                        int sightMode = (sightComponent.ScopesSelectedModes.Length != sightComponent.ScopesCount) ? 0 : sightComponent.ScopesSelectedModes[i];
                        int scopeCalibrationIndex = (sightComponent.ScopesCurrentCalibPointIndexes.Length != sightComponent.ScopesCount) ? 0 : sightComponent.ScopesCurrentCalibPointIndexes[i];
                        sightStructList.Add(new FirearmScopeStateStruct
                        {
                            Id = sightComponent.Item.Id,
                            ScopeIndexInsideSight = i,
                            ScopeMode = ((index == aimIndex) ? (sightMode + 1) : sightMode),
                            ScopeCalibrationIndex = scopeCalibrationIndex
                        });
                        index++;
                    }
                }
            }
            return sightStructList.ToArray();
        }

        private static FirearmScopeStateStruct[] doVuduZoom(Weapon weapon, Player player)
        {
            //you can thank BSG for this monstrosity 
            IEnumerable<SightComponent> sightEnumerable = Enumerable.OrderBy<SightComponent, string>(Enumerable.Select<Slot, Item>(weapon.AllSlots, new Func<Slot, Item>(getContainedItem)).GetComponents<SightComponent>(), new Func<SightComponent, string>(getSightComp));
            List<FirearmScopeStateStruct> sightStructList = new List<FirearmScopeStateStruct>();
            int aimIndex = weapon.AimIndex.Value;

            foreach (SightComponent sightComponent in sightEnumerable)
            {
                if (hasScopeAimBone(sightComponent, player))
                {
                    for (int i = 0; i < sightComponent.ScopesCount; i++)
                    {
                        int index = Plugin.FovController.CurrentZoom == 1f ? (int)Plugin.FovController.CurrentZoom - 1 : Plugin.FovController.CurrentZoom == 1.5f ? 1 : (int)Plugin.FovController.CurrentZoom;
                        int scopeCalibrationIndex = (sightComponent.ScopesCurrentCalibPointIndexes.Length != sightComponent.ScopesCount) ? 0 : sightComponent.ScopesCurrentCalibPointIndexes[i];
                        sightStructList.Add(new FirearmScopeStateStruct
                        {
                            Id = sightComponent.Item.Id,
                            ScopeIndexInsideSight = 0,
                            ScopeMode = index,
                            ScopeCalibrationIndex = scopeCalibrationIndex
                        });
                    }
                }
            }
            return sightStructList.ToArray();
        }

        [PatchPostfix]
        private static void PatchPostfix(Player.FirearmController __instance, bool __result)
        {
            Player player = (Player)playerField.GetValue(__instance);
            if (player != null && player.MovementContext.CurrentState.Name != EPlayerState.Stationary && player.IsYourPlayer)
            {
                Plugin.FovController.IsAiming = __result;

                if (Plugin.EnableVariableZoom.Value && Plugin.FovController.IsAiming && (!hasSetFov || Plugin.FovController.ChangeSight || (Plugin.FovController.DidToggleForFirstPlane && Plugin.SamSwatVudu.Value && Plugin.FovController.CurrentScopeTempID == "5b3b99475acfc432ff4dcbee")))
                {
                    Plugin.FovController.ChangeSight = false;
                    ProceduralWeaponAnimation pwa = player.ProceduralWeaponAnimation;
                    if (pwa.CurrentScope.IsOptic)
                    {
                        Plugin.FovController.IsOptic = true;
                        adsTimer += Time.deltaTime;
                        if (adsTimer >= 0.5f)
                        {
                            hasSetFov = true;
                            Mod currentAimingMod = (pwa.CurrentAimingMod != null) ? pwa.CurrentAimingMod.Item as Mod : null;
                            SightModClass sightModClass = currentAimingMod as SightModClass;
                            SightComponent sightComp = player.ProceduralWeaponAnimation.CurrentAimingMod;
                            SightComptInterface sightCompInter = (SightComptInterface)sightComptField.GetValue(sightModClass.Sight);
                            Plugin.FovController.IsFixedMag = currentAimingMod.Template.HasShoulderContact;
                            Plugin.FovController.CanToggle = Plugin.AllowReticleToggle.Value ? true :  currentAimingMod.Template.ToolModdable;
                            Plugin.FovController.CanToggleButNotFixed = Plugin.FovController.CanToggle && !Plugin.FovController.IsFixedMag;
                            float minZoom = 1f;
                            float maxZoom = 1f;

                            if (Plugin.FovController.IsFixedMag)
                            {
                                minZoom = sightCompInter.Zooms[0][0];
                                maxZoom = minZoom;
                            }
                            else if (currentAimingMod.TemplateId == "5b3b99475acfc432ff4dcbee" && Plugin.SamSwatVudu.Value) 
                            {
                                minZoom = sightCompInter.Zooms[0][0];
                                maxZoom = sightCompInter.Zooms[0][6];
                            }
                            else if (Plugin.FovController.CanToggleButNotFixed && sightCompInter.Zooms[0].Length > 2)
                            {
                                minZoom = sightCompInter.Zooms[0][0];
                                maxZoom = sightCompInter.Zooms[0][2];
                            }
                            else if (sightCompInter.Zooms[0][0] > sightCompInter.Zooms[0][1])
                            {
                                maxZoom = sightCompInter.Zooms[0][0];
                                minZoom = sightCompInter.Zooms[0][1];
                            }
                            else
                            {
                                minZoom = sightCompInter.Zooms[0][0];
                                maxZoom = sightCompInter.Zooms[0][1];
                            }

                            Plugin.FovController.IsFucky = (minZoom < 2 && sightComp.SelectedScopeIndex == 0 && sightComp.SelectedScopeMode == 0 && !Plugin.FovController.IsFixedMag && !Plugin.FovController.CanToggle && currentAimingMod.TemplateId != "5b2388675acfc4771e1be0be");
                            bool isSamVudu = currentAimingMod.TemplateId == "5b3b99475acfc432ff4dcbee" && Plugin.SamSwatVudu.Value;
                            if (Plugin.FovController.IsFucky && !isSamVudu)
                            {
                                __instance.SetScopeMode(getScopeModeFullList(__instance.Item, player));
                            }

                            if (Plugin.FovController.DidToggleForFirstPlane && Plugin.SamSwatVudu.Value && currentAimingMod.TemplateId == "5b3b99475acfc432ff4dcbee")
                            {
                                __instance.SetScopeMode(doVuduZoom(__instance.Item, player));
                                Plugin.FovController.DidToggleForFirstPlane = false;
                            }

                            Plugin.FovController.MinZoom = minZoom;
                            Plugin.FovController.MaxZoom = maxZoom;

                            Plugin.FovController.CurrentWeapInstanceID = __instance.Item.Id.ToString();
                            Plugin.FovController.CurrentScopeInstanceID = pwa.CurrentAimingMod.Item.Id.ToString();

                            bool weapExists = true;
                            bool scopeExists = false;
                            float rememberedZoom = minZoom;

                            if (!Plugin.FovController.WeaponScopeValues.ContainsKey(Plugin.FovController.CurrentWeapInstanceID))
                            {
                                weapExists = false;
                                Plugin.FovController.WeaponScopeValues[Plugin.FovController.CurrentWeapInstanceID] = new List<Dictionary<string, float>>();
                            }

                            List<Dictionary<string, float>> scopes = Plugin.FovController.WeaponScopeValues[Plugin.FovController.CurrentWeapInstanceID];
                            foreach (Dictionary<string, float> scopeDict in scopes)
                            {
                                if (scopeDict.ContainsKey(Plugin.FovController.CurrentScopeInstanceID))
                                {
                                    rememberedZoom = scopeDict[Plugin.FovController.CurrentScopeInstanceID];
                                    scopeExists = true;
                                    break;
                                }
                            }

                            if (!scopeExists)
                            {
                                Dictionary<string, float> newScope = new Dictionary<string, float>
                                {
                                  { Plugin.FovController.CurrentScopeInstanceID, minZoom }
                                };
                                Plugin.FovController.WeaponScopeValues[Plugin.FovController.CurrentWeapInstanceID].Add(newScope);
                            }

                            bool isElcan = Plugin.FovController.IsFixedMag && Plugin.FovController.CanToggle;
                            if (!isElcan && (Plugin.FovController.IsFixedMag || !weapExists || !scopeExists))
                            {
                                Plugin.FovController.CurrentZoom = minZoom;
                                Plugin.FovController.ZoomScope(minZoom);
                            }

                            if (weapExists && scopeExists)
                            {
                                Plugin.FovController.CurrentZoom = rememberedZoom;
                                Plugin.FovController.ZoomScope(rememberedZoom);
                            }

                            if (isElcan)
                            {
                                float currentToggle = player.ProceduralWeaponAnimation.CurrentAimingMod.GetCurrentOpticZoom();
                                Plugin.FovController.CurrentZoom = currentToggle;
                                Plugin.FovController.ZoomScope(currentToggle);
                            }
                        }
                    }
                    else
                    {
                        Plugin.FovController.CurrentZoom = 1f;
                    }
                }
                else if (!Plugin.FovController.IsAiming)
                {
                    adsTimer = 0f;
                    hasSetFov = false;
                }
            }
        }
    }


    public class FreeLookPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod("Look", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static void Prefix(Player __instance, float deltaLookY, float deltaLookX, bool withReturn = true)
        {
            //credit to CJ for making me realize I didn't have to write this giant ass patch like an idiot        
            EFTHardSettings.Instance.MOUSE_LOOK_HORIZONTAL_LIMIT = new Vector2(-50f, 50f); 
        }
    }


    public class LerpCameraPatch : ModulePatch
    {
        private static FieldInfo _playerField;
        private static FieldInfo _fcField;

        private static float _yPos = 0f;

        protected override MethodBase GetTargetMethod()
        {
            _playerField = AccessTools.Field(typeof(FirearmController), "_player");
            _fcField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmController");
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("LerpCamera", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool Prefix(EFT.Animations.ProceduralWeaponAnimation __instance, float dt, float ____overweightAimingMultiplier, float ____aimingSpeed, float ____aimSwayStrength, Player.ValueBlender ____aimSwayBlender, Vector3 ____aimSwayDirection, Vector3 ____headRotationVec, Vector3 ____vCameraTarget, Player.ValueBlenderDelay ____tacticalReload, Quaternion ____cameraIdenity, Quaternion ____rotationOffset)
        {
            FirearmController firearmController = (FirearmController)_fcField.GetValue(__instance);
            if (firearmController == null) return true;
            Player player = (Player)_playerField.GetValue(firearmController);
            if (player != null && player.IsYourPlayer && firearmController.Weapon != null)
            {
                float headBob = Singleton<SharedGameSettingsClass>.Instance.Game.Settings.HeadBobbing;
                Vector3 localPosition = __instance.HandsContainer.CameraTransform.localPosition;
                float localX = localPosition.x;
                float localY = localPosition.y;
                float localZ = localPosition.z;

                float camX = ____vCameraTarget.x;
                float camY = ____vCameraTarget.y;
                float camZ = __instance.IsAiming && !Plugin.FovController.IsOptic && Plugin.IsPistol ? ____vCameraTarget.z - Plugin.PistolOffset.Value : __instance.IsAiming && !Plugin.FovController.IsOptic ? ____vCameraTarget.z - Plugin.NonOpticOffset.Value : __instance.IsAiming && Plugin.FovController.IsOptic ? ____vCameraTarget.z - Plugin.OpticPosOffset.Value : ____vCameraTarget.z;

                float smoothTime = Plugin.FovController.IsOptic ? Plugin.OpticAimSpeed.Value * dt : Plugin.IsPistol ? Plugin.PistolAimSpeed.Value * dt : Plugin.CameraAimSpeed.Value * dt;

                float aimFactorX = __instance.IsAiming ? (____aimingSpeed * __instance.CameraSmoothBlender.Value * ____overweightAimingMultiplier) * Plugin.AimSpeedX.Value : Plugin.UnAimSpeedX.Value;
                float aimFactorY = __instance.IsAiming ? (____aimingSpeed * __instance.CameraSmoothBlender.Value * ____overweightAimingMultiplier) * Plugin.AimSpeedY.Value : Plugin.UnAimSpeedY.Value;
                float aimFactorZ = __instance.IsAiming ? (1f + __instance.HandsContainer.HandsPosition.GetRelative().y * 100f + __instance.TurnAway.Position.y * 10f) * Plugin.AimSpeedZ.Value : Plugin.UnAimSpeedZ.Value;

                float targetX = Mathf.Lerp(localX, camX, smoothTime * aimFactorX);
                float targetY = Mathf.Lerp(localY, camY, smoothTime * aimFactorY);
                float targetZ = Mathf.Lerp(localZ, camZ, smoothTime * aimFactorZ);

                Vector3 newLocalPosition = new Vector3(targetX, targetY, targetZ) + __instance.HandsContainer.CameraPosition.GetRelative();
                
                if (____aimSwayStrength > 0f)
                {
                    float blendValue = ____aimSwayBlender.Value; 
                    if (__instance.IsAiming && blendValue > 0f)
                    {
                        __instance.HandsContainer.SwaySpring.ApplyVelocity(____aimSwayDirection * blendValue);
                    }
                }

                if (Plugin.RealismIsPresent && Plugin.IsPistol && Plugin.RealismAltPistol.Value) _yPos = Mathf.Max(newLocalPosition.y, 0.035f);
                else if (Plugin.RealismIsPresent && Plugin.RealismAltRifle.Value) 
                {
       /*             float limit = newLocalPosition.y < 0f && __instance.IsAiming ? camY * Plugin.test1.Value : Mathf.Max(newLocalPosition.y, -0.015f);*/
                    float target = Mathf.Max(newLocalPosition.y, -0.015f); //!__instance.IsAiming ? Mathf.Max(newLocalPosition.y, -0.015f) : 
                    _yPos = target;
                }
                else _yPos = newLocalPosition.y;

                __instance.HandsContainer.CameraTransform.localPosition = new Vector3(newLocalPosition.x, _yPos, newLocalPosition.z);
                Quaternion animatedRotation = __instance.HandsContainer.CameraAnimatedFP.localRotation * __instance.HandsContainer.CameraAnimatedTP.localRotation;
                __instance.HandsContainer.CameraTransform.localRotation = Quaternion.Lerp(____cameraIdenity, animatedRotation, headBob * (1f - ____tacticalReload.Value)) * Quaternion.Euler(__instance.HandsContainer.CameraRotation.Get() + ____headRotationVec) * ____rotationOffset;
                __instance.method_19(dt);
                __instance.HandsContainer.CameraTransform.localEulerAngles += __instance.Shootingg.CurrentRecoilEffect.GetCameraRotationRecoil();

                //hud fov
                __instance.HandsContainer.CameraOffset = new Vector3(0.04f, 0.04f, Plugin.HudFOV.Value);

                return false;
            }
            return true;
        }
    }

    public class PwaWeaponParamsPatch : ModulePatch
    {
        private static FieldInfo playerField;
        private static FieldInfo fcField;

        protected override MethodBase GetTargetMethod()
        {
            playerField = AccessTools.Field(typeof(FirearmController), "_player");
            fcField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmController");
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("method_23", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref EFT.Animations.ProceduralWeaponAnimation __instance)
        {
            FirearmController firearmController = (FirearmController)fcField.GetValue(__instance);
            if (firearmController == null) return;
            Player player = (Player)playerField.GetValue(firearmController);
            if (player != null && player.IsYourPlayer)
            {
                Plugin.IsPistol = firearmController.Weapon.WeapClass == "pistol";
                Plugin.FovController.SetToggleZoomMulti();
                Plugin.FovController.DoFov();          
                
            }
        }
    }

    public class OpticSightAwakePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.CameraControl.OpticSight).GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool Prefix(EFT.CameraControl.OpticSight __instance)
        {

            __instance.TemplateCamera.gameObject.SetActive(false);
            if (__instance.name != "DONE")
            {
                if (Plugin.TrueOneX.Value == true && __instance.TemplateCamera.fieldOfView >= 24)
                {
                    return false;
                }
                __instance.TemplateCamera.fieldOfView *= Plugin.GlobalOpticFOVMulti.Value;
                __instance.name = "DONE";
            }
            return false;
        }
    }

    public class OnWeaponParametersChangedPatch : ModulePatch
    {
        private static FieldInfo weaponField;

        protected override MethodBase GetTargetMethod()
        {
            weaponField = AccessTools.Field(typeof(ShotEffector), "_weapon");
            return typeof(ShotEffector).GetMethod("OnWeaponParametersChanged", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(ShotEffector __instance)
        {
            IWeapon _weapon = (IWeapon)weaponField.GetValue(__instance);
            if (_weapon?.Item?.Owner.ID != null && _weapon.Item.Owner.ID == Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId)
            {
                Plugin.FovController.HasRAPTAR = false;

                if (!_weapon.IsUnderbarrelWeapon)
                {
                    Weapon weap = _weapon.Item as Weapon;
                    IEnumerable<Mod> weapMods = weap.Mods;
                    foreach (Mod mod in weapMods)
                    {
                        if (mod.TemplateId == "61605d88ffa6e502ac5e7eeb")
                        {
                            Plugin.FovController.HasRAPTAR = true;
                        }
                    }
                }

            }
        }
    }


    public class TacticalRangeFinderControllerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TacticalRangeFinderController).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {

            if (Plugin.FovController.HasRAPTAR == false)
            {
                CameraClass.Instance.OpticCameraManager.Camera.fieldOfView = Plugin.RangeFinderFOV.Value;
            }

        }
    }


    //better to do it in method_17Patch, as this method also sets FOV in general.
    /*    public class SetFovPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(CameraClass).GetMethod("SetFov", BindingFlags.Instance | BindingFlags.Public);
            }

            [PatchPrefix]
            private static bool Prefix(CameraClass __instance, ref float x, float time, Coroutine ___coroutine_0, bool applyFovOnCamera = true)
            {

                var _method_4 = AccessTools.Method(typeof(CameraClass), "method_4");
                float fov = x * Plugin.globalADSMulti.Value;

                if (___coroutine_0 != null)
                {
                    StaticManager.KillCoroutine(___coroutine_0);
                }
                if (__instance.Camera == null)
                {
                    return false;
                }
                IEnumerator meth4Enumer = (IEnumerator)_method_4.Invoke(__instance, new object[] { fov, time });
                AccessTools.Property(typeof(CameraClass), "ApplyDovFovOnCamera").SetValue(__instance, applyFovOnCamera);
                ___coroutine_0 = StaticManager.BeginCoroutine(meth4Enumer);
                return false;
            }
        }*/

}
