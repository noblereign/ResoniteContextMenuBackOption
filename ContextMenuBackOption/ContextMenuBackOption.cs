using System;
using System.Collections.Generic;

using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
#if DEBUG
using ResoniteHotReloadLib;
#endif
using FrooxEngine.UIX;
using System.Threading.Tasks;

namespace ContextMenuBackOption;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
//Mod code partially based on https://github.com/XDelta/ResoniteFish (because it's a very simple context menu option mod lol)
public class ContextMenuBackOption : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.1.1";
	public override string Name => "ContextMenuBackOption";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/ResoniteContextMenuBackOption";
	const string harmonyId = "dog.glacier.ContextMenuBackOption";

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Enables the mod. Pretty self explanatory!", () => true);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ShowOnBuiltIn = new("Show on built-in submenus", "Should the Back option show up on built-in submenus? (e.g. the Locomotion submenu)", () => false);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ShowOnSingleItemMenus = new("Show on single item menus", "Should the Back option show up on single item menus? Turning this on may break some systems that rely on having only one button present (for example, certain context menu sliders).", () => false);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> AlternateDesign = new("Alternate Design", "Instead of adding a back option, adds a new button between the empty space in the context menu.\n\n<color=yellow>NOTE:</color> With this setting enabled, the mod will hide existing back buttons, so as to not have duplicates.", () => false);
	//Alternate design suggested by U-PearPaw

	public static ModConfiguration? Config;

	static List<Slot> PreviousMenus = new();
	static float lastLerp = 0;
	static float lastInnerLerp = 0;

	static Type INTERACTION_HANDLER_TYPE = TypeHelper.FindType("FrooxEngine.InteractionHandler");

	public override void OnEngineInit() {
		#if DEBUG
		HotReloader.RegisterForHotReload(this);
		#endif

		Config = GetConfiguration()!;
		Config!.Save(true);

		// Call setup method
		Setup();
	}
	#if DEBUG
	// This is the method that should be used to unload your mod
	// This means removing patches, clearing memory that may be in use etc.
	static void BeforeHotReload() {
		// Unpatch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.UnpatchAll(harmonyId);

		// clear any memory that is being used
		PreviousMenus.Clear();
		lastLerp = 0;
		lastInnerLerp = 0;
	}

	// This is called in the newly loaded assembly
	// Load your mod here like you normally would in OnEngineInit
	
	static void OnHotReload(ResoniteMod modInstance) {
		// Get the config if needed
		Config = modInstance.GetConfiguration()!;
		Config!.Save(true);

		// Call setup method
		Setup();
	}
	#endif

	static void Setup() {
		// Patch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.PatchAll();
	}

	static void RemovePreviousPage(IButton button, ButtonEventData eventData) {
		Debug("Clicked, removing entries...");
		int removed = 0;
		while (removed < 2) {
			if (PreviousMenus.Count <= 0) {
				break;
			}
			PreviousMenus.RemoveAt(0);
			removed++;
			Debug(removed + "removed");
		}
	}

	public static string TrimDescription(string description) {
		if (description.Length <= 32) {
			return description;
		}

		return description.Substring(0, 29) + "...";
	}

	static void GeneralButtonHandler(User user, IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions)) {
		ContextMenu menu = user.GetUserContextMenu();
		if (menu != null) {
			Debug("Trying for fancy button");
			Debug(user);
			Debug("menu:");
			Debug(menu);
			Tuple<Slot, Button>? FancyItems = TryFancyButton(menu);
			Slot? FancyButton = null;
			IButton? Button = null;
			Debug("Tried!");

			if (FancyItems != null) {
				Debug("Fancy button path");
				FancyButton = FancyItems.Item1;
				Button = FancyItems.Item2;
			}

			Slot itemRoot = menu._itemsRoot.Target;
			if (itemRoot.ChildrenCount <= 1) {
				Debug("Single button menu!");
				if (!(Config!.GetValue(ShowOnSingleItemMenus))) {
					FancyButton.ActiveSelf = false;
					return;
				}
			}
			
			if (FancyButton != null) {
				FancyButton.ActiveSelf = (PreviousMenus.Count > 0);
			}
			Debug("Fancy button active checked");
			if (PreviousMenus.Count > 0) {
				IButton? existingButton = null;
				Slot? existingContextMenuButton = null;
				if (PreviousMenus.Count > 1) { // As far as I know you can't link back to the root menu in vanilla game... i think...
					foreach (Slot child in PreviousMenus[0].Children) {
						ContextMenuSubmenu? submenu = child.GetComponent<ContextMenuSubmenu>();
						ContextMenuItemSource? source = child.GetComponent<ContextMenuItemSource>();

						if (submenu != null && source != null && (submenu.ItemsRoot.Target == PreviousMenus[1])) {
							Debug("Existing back button found, hooking!");
							existingButton = source;
							break;
						}
					}

					if (existingButton != null) {
						Debug("Finding arc layout");
						Slot? ArcLayout = menu.Slot.FindChild("ArcLayout", false, false, 3);
						RefID existingRef = existingButton.Slot.ReferenceID;
						Debug(existingRef);
						if (ArcLayout != null) {
							Debug("Seearching ctx");
							foreach (Slot child in ArcLayout.Children) {
								ButtonPressEventRelay? eventRelay = child.GetComponent<ButtonPressEventRelay>();

								if (eventRelay != null && (eventRelay.Target.Value == existingRef)) {
									existingContextMenuButton = eventRelay.Slot;
									break;
								}
							}
						}
					}
				}


				Debug("Checked for back buttons");
				
				bool previousIsRoot = false;
				if (PreviousMenus.Count > 1) {
					previousIsRoot = (PreviousMenus[1] == user.GetUserContextMenu().Slot);
				} else {
					previousIsRoot = true;
				}

				if (Button == null && existingButton == null) {
					Debug("Making own back button...");

					string localized = user.GetLocalized("General.Back");
					localized += "\n<size=50%>";
					if (previousIsRoot == true) {
						localized += user.GetLocalized("Dash.Screens.Home");
					} else {
						localized += TrimDescription(PreviousMenus[1].NameField.Value);
					}
					localized += "</size>";

					ContextMenuItem MenuItem = menu.AddItem(localized, new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png"), colorX.White);
					Slot MenuSlot = MenuItem.Slot;
					MenuSlot.Tag = "BackOption";
					MenuSlot.OrderOffset = -10000;

					Button = MenuItem.Button;

					if ((PreviousMenus.Count > 1) && !previousIsRoot) {
						ButtonPressEventRelay BackRelay = MenuSlot.AttachComponent<ButtonPressEventRelay>();
						BackRelay.Target.Value = PreviousMenus[1].ReferenceID;
					}
				} else if (FancyButton != null && Button != null && Button.IsChildOfElement(FancyButton)) {
					if (Config!.GetValue(AlternateDesign) == true) {
						ButtonPressEventRelay? BackRelay = (ButtonPressEventRelay?)FancyButton.GetComponentOrAttach<ButtonPressEventRelay>();
						if (BackRelay != null) {
							if (existingButton != null) { // use existing button relay and hide
								Debug("using existing relay");
								BackRelay.Target.Value = existingButton.Slot.ReferenceID;
								if (existingContextMenuButton != null) {
									existingContextMenuButton.ActiveSelf = false;
								}
							} else if ((PreviousMenus.Count > 1) && !previousIsRoot) { // previous page
								BackRelay.Target.Value = PreviousMenus[1].ReferenceID;
							} else { // to root
								BackRelay.Target.Value = RefID.Null;
							}
						} else {
							Warn("Could not find button relay!");
						}
					}
				} else {
					Button = existingButton;
					if (existingContextMenuButton != null) {
						existingContextMenuButton.Tag = "BackOption";
					}
				}

				Button.LocalPressed -= RemovePreviousPage; // C# why do i need this
				if ((PreviousMenus.Count > 1) && !previousIsRoot) {
					Debug("Previous menu");
					Button.LocalPressed += RemovePreviousPage;
					Debug("Added");
				} else {
					Debug("Root menu");
					Button.LocalPressed += (IButton button, ButtonEventData eventData) => {
						if (previousIsRoot) {
							RemovePreviousPage(button, eventData);
						}
						Debug("Going back to root!");
						if (summoner.GetType() == INTERACTION_HANDLER_TYPE) {
							((InteractionHandler)summoner).OpenContextMenu(InteractionHandler.MenuOptions.Default);
						} else {
							Warn("Summoner was not interaction handler, falling back to primary hand");
							user.GetInteractionHandler(user.Primaryhand).OpenContextMenu(InteractionHandler.MenuOptions.Default);
						}
					};
					Debug("Root menu fully added");
				}
			}
		}
	}

	public static async Task HandleButtonAfterAnimation(ContextMenu menu, User user, IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions)) {
		Debug("Waiting...");
		while ((menu._lerp.Value > 0f) && menu != null) {
			await default(NextUpdate);
		}
		if (menu != null) {
			Debug("Button will now be handled.");
			GeneralButtonHandler(user, summoner, pointer, options);
		} else {
			Warn("Cannot handle button, menu became null!");
		}
	}

	static Button SetupFancyButtonComponent(Slot FancyButton, Image image, OutlinedArc outlinedArc) {
		Button button = FancyButton.AttachComponent<Button>();
		button.RequireInitialPress.Value = false;
		button.PassThroughHorizontalMovement.Value = false;
		button.PassThroughVerticalMovement.Value = false;

		InteractionElement.ColorDriver colorDriverImage = button.ColorDrivers.Add();
		colorDriverImage.ColorDrive.Target = image.Tint;
		colorDriverImage.NormalColor.Value = colorX.White;
		colorDriverImage.HighlightColor.Value = colorX.White;
		colorDriverImage.PressColor.Value = colorX.White;
		colorDriverImage.DisabledColor.Value = colorX.White.SetA(0.53f);

		colorX color = RadiantUI_Constants.Neutrals.LIGHT;
		InteractionElement.ColorDriver colorDriver = button.ColorDrivers.Add();
		InteractionElement.ColorDriver colorDriver2 = button.ColorDrivers.Add();
		ColorHSV colorHSV = new ColorHSV(in color);
		colorDriver.NormalColor.Value = RadiantUI_Constants.BG_COLOR;
		colorDriver.HighlightColor.Value = RadiantUI_Constants.GetTintedButton(color);
		colorDriver.PressColor.Value = RadiantUI_Constants.GetTintedButton(color).MulRGB(3f);
		colorDriver.DisabledColor.Value = RadiantUI_Constants.DISABLED_COLOR.SetA(0.5f);
		colorDriver2.NormalColor.Value = new ColorHSV(colorHSV.h, MathX.Min(colorHSV.s * 1.5f, 0.9f), 1.5f).ToRGB(color.profile);
		colorDriver2.HighlightColor.Value = new ColorHSV(colorHSV.h, MathX.Min(colorHSV.s, 0.6f), 3f).ToRGB(color.profile);
		colorDriver2.PressColor.Value = new ColorHSV(colorHSV.h, MathX.Min(colorHSV.s, 0.5f), 6f).ToRGB(color.profile);
		colorDriver2.DisabledColor.Value = new colorX(0.5f, 0.75f);

		colorDriver.ColorDrive.Target = outlinedArc.FillColor;
		colorDriver2.ColorDrive.Target = outlinedArc.OutlineColor;

		return button;
	}

	static Slot ConstructFancyButton(Slot RadialMenu) {
		Slot FancyButton = RadialMenu.AddSlot("CtxMenuBack");
		RectTransform rectTransform = FancyButton.AttachComponent<RectTransform>();
		rectTransform.AnchorMin.Value = new float2(0.25f, 0.25f);
		rectTransform.AnchorMax.Value = new float2(0.75f, 0.75f);
		OutlinedArc outlinedArc = FancyButton.AttachComponent<OutlinedArc>();

		UI_CircleSegment arcMaterial = RadialMenu.Parent.Parent.GetComponent<UI_CircleSegment>();

		outlinedArc.Arc.Value = 80f;
		outlinedArc.Offset.Value = 230f;
		outlinedArc.InnerRadiusRatio.Value = 0.6f;
		outlinedArc.OuterRadiusRatio.Value = 1f;
		outlinedArc.RoundedCornerRadius.Value = 14f;
		outlinedArc.OutlineThickness.Value = 2f;
		outlinedArc.Material.Target = arcMaterial;

		Slot ButtonIcon = FancyButton.AddSlot("Icon");
		RectTransform rectTransform2 = ButtonIcon.AttachComponent<RectTransform>();
		rectTransform2.AnchorMin.Value = new float2(0f, 0.025f);
		rectTransform2.AnchorMax.Value = new float2(1f, 0.175f);

		UI_UnlitMaterial imageMaterial = RadialMenu.Parent.Parent.GetComponent<UI_UnlitMaterial>();

		SpriteProvider spriteProvider = ButtonIcon.AttachComponent<SpriteProvider>();
		StaticTexture2D texture = ButtonIcon.AttachComponent<StaticTexture2D>();
		texture.URL.Value = new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png");
		spriteProvider.Texture.Target = texture;

		Image image = ButtonIcon.AttachComponent<Image>();
		image.Sprite.Target = spriteProvider;
		image.Material.Target = imageMaterial;

		SetupFancyButtonComponent(FancyButton, image, outlinedArc);
		return FancyButton;
	}

	private static Tuple<Slot, Button>? TryFancyButton(ContextMenu menu) {
		Slot? RadialMenu = menu.Slot.FindChild("Radial Menu", false, false, 2);
		if (RadialMenu != null) {
			Debug("Found radial, now look for button");
			Slot? FancyButton = RadialMenu.FindChild("CtxMenuBack", false, false, 1);
			Button? buttonComponent = null;
			Debug("Button:");
			Debug(FancyButton);
			if (FancyButton == null) { // There's probably a way to do this 100,000,000x better but I dunno how so :3
				Debug("No button found");
				if (Config!.GetValue(Enabled) == true && Config!.GetValue(AlternateDesign) == true) {
					// Manually construct the UI (horrible)
					FancyButton = ConstructFancyButton(RadialMenu);
					buttonComponent = FancyButton.GetComponent<Button>();
					Debug("Made fancy button");
				} else {
					return null;
				}
			} else if (Config!.GetValue(Enabled) == true && Config!.GetValue(AlternateDesign) == true) {
				Debug("Existing button found, look for icon");
				Slot? FancyButtonImage = FancyButton.FindChild("Icon", false, false, 1);
				if (FancyButtonImage != null) {
					Debug("we found the icon");
					buttonComponent = FancyButton.GetComponent<Button>();
					Debug("Button component:");
					Debug(buttonComponent);
					if (buttonComponent != null) { // kinda hacky and i wish i didn't need to: re-create the button to make sure the LocalPressed events are always cleared
						Debug("Destorying button </3");
						try {
							buttonComponent.Destroy();
						} catch (Exception e) {
							Warn(e);
						}
					}
					Debug("btutton gone");
					Image fancyImage = FancyButtonImage.GetComponent<Image>();
					OutlinedArc fancyArc = FancyButton.GetComponent<OutlinedArc>();

					Debug("Releasing links");
					if (fancyImage.Tint.ActiveLink != null) {
						fancyImage.Tint.ReleaseLink(fancyImage.Tint.ActiveLink);
					}
					if (fancyArc.FillColor.ActiveLink != null) {
						fancyArc.FillColor.ReleaseLink(fancyArc.FillColor.ActiveLink);
					}
					if (fancyArc.FillColor.ActiveLink != null) {
						fancyArc.FillColor.ReleaseLink(fancyArc.FillColor.ActiveLink);
					}
					Debug("Done, setting up fancy component");
					buttonComponent = SetupFancyButtonComponent(FancyButton, fancyImage, fancyArc);
					Debug("ohhh yeahhh");
				} else {
					Warn("Could not find fancy button image, recreating the button entirely!");
					FancyButton.Destroy();
					FancyButton = ConstructFancyButton(RadialMenu);
					buttonComponent = FancyButton.GetComponent<Button>();
				}
			} else {
				Debug("Returning null");
				FancyButton?.Destroy();
				return null;
			}
			Debug("Returning tuple");
			return new Tuple<Slot, Button>(FancyButton, buttonComponent);
		}
		Debug("Truly null");
		return null;
	}
	static void UpdateFancyButtonVisuals(ContextMenu ctx) {
		Slot? RadialMenu = ctx.Slot.FindChild("Radial Menu", false, false, 2);
		if (RadialMenu != null) {
			Slot? FancyButton = RadialMenu.FindChild("CtxMenuBack", false, false, 1);
			if (FancyButton != null) {
				Slot? FancyButtonIcon = FancyButton.FindChild("Icon", false, false, 1);
				if (FancyButtonIcon != null) {
					Slot? CenterCircle = RadialMenu.FindChild("Center Circle", false, false, 1); // is there a better way to do this?
					if (CenterCircle != null) {
						OutlinedArc fancyArc = FancyButton.GetComponent<OutlinedArc>();
						RectTransform fancyIconTransform = FancyButtonIcon.GetComponent<RectTransform>();
						RectTransform centerTransform = CenterCircle.GetComponent<RectTransform>();
						if (fancyArc != null && centerTransform != null) {
							float innerLerp = 0;
							if (ctx._innerLerp != null) {
								innerLerp = ctx._innerLerp.Value;
							}
							if (innerLerp >= 0) {
								fancyArc.InnerRadiusRatio.Value = MathX.Remap(ctx.Lerp, 0f, 1f, .675f, .6f) + MathX.Remap(innerLerp, 0f, 1f, 0f, .2f);
								fancyArc.OuterRadiusRatio.Value = MathX.Remap(ctx.Lerp, 0f, 1f, .925f, 1f);
								fancyArc.RoundedCornerRadius.Value = MathX.Remap(innerLerp, 0f, 1f, 14f, 8f);
								fancyArc.Arc.Value = MathX.Remap(innerLerp, 0f, 1f, 80f, 40f);
								fancyArc.Offset.Value = MathX.Remap(innerLerp, 0f, 1f, 230f, 250f);
								fancyIconTransform.AnchorMin.Value = new float2(0f, 0.025f);
								fancyIconTransform.AnchorMax.Value = new float2(MathX.Remap(innerLerp, 0f, 1f, 1f, 1f), MathX.Remap(innerLerp, 0f, 1f, 0.175f, 0.075f));
							} else {
								fancyArc.InnerRadiusRatio.Value = .6f;
								fancyArc.OuterRadiusRatio.Value = 1f;
								fancyArc.RoundedCornerRadius.Value = 14f;
								fancyArc.Arc.Value = MathX.Remap(innerLerp, -1f, 0f, 0f, 80f);
								fancyArc.Offset.Value = MathX.Remap(innerLerp, -1f, 0f, 270f, 230f);
								fancyIconTransform.AnchorMin.Value = new float2(MathX.Remap(innerLerp, -1f, 0f, 0f, 0f), MathX.Remap(innerLerp, -1f, 0f, 0.1f, 0.025f));
								fancyIconTransform.AnchorMax.Value = new float2(MathX.Remap(innerLerp, -1f, 0f, 1f, 1f), MathX.Remap(innerLerp, -1f, 0f, 0.1f, 0.175f));
							}
						}
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(InteractionHandler), "OpenContextMenu")]
	class ContextMenuOpenRootPatch {
		public static bool Prefix(InteractionHandler __instance, InteractionHandler.MenuOptions options, out bool __state) { // This one fires for Context Menu Root as well as the built-in ones (e.g. Locomotion, Grab Type)
			__state = false;
			if (__instance.IsOwnedByLocalUser) {
				if (options == InteractionHandler.MenuOptions.Default || (Config!.GetValue(ShowOnBuiltIn) == false)) {
					Debug("Context menu root opened, clear previous menus");
					PreviousMenus.Clear();
				} else if (Config!.GetValue(ShowOnBuiltIn)) {
					Debug("Built in context menu opened, add root back");
					PreviousMenus.Insert(0, __instance.LocalUser.GetUserContextMenu().Slot); // Use context menu slot as placeholder for "Root Menu"
					__state = true;
				}
				Tuple<Slot, Button>? FancyItems = TryFancyButton(__instance.LocalUser.GetUserContextMenu());
				if (FancyItems != null) {
					FancyItems.Item1.ActiveSelf = (PreviousMenus.Count > 0);
				}
			}
			return true;
		}

		public static async void Postfix(InteractionHandler __instance, InteractionHandler.MenuOptions options, bool __state) { // This one fires for Context Menu Root as well as the built-in ones (e.g. Locomotion, Grab Type)
			if (__state) {
				Debug("Attempting button handler.");
				await __instance.World.Coroutines.StartTask(async delegate
				{
					await HandleButtonAfterAnimation(__instance.LocalUser.GetUserContextMenu(), __instance.LocalUser, __instance, __instance.PointReference, default(ContextMenuOptions));
				}); 
				Debug("Button handled.");
			}
		}
	}

	[HarmonyPatch(typeof(ContextMenuExtensions), "ContextMenuConfirm")]
	class ContextMenuConfirmActionPatch {
		public static bool Prefix(User user, IWorldElement summoner, Slot pointer, LocaleString actionName, Uri actionIcon, colorX actionColor, ButtonEventHandler actionCallback, bool hidden = false) { // This fires for actions that require confirmation (e.g. entering an anchor, equipping an avatar/tool...)
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					Debug("Confirmation opened, clear previous pages");
					PreviousMenus.Clear();
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(WorldOrb), "ToggleContextMenu")]
	class WorldOrbContextMenuPatch {
		public static bool Prefix(WorldOrb __instance, TouchEventInfo touchInfo) { // This fires when you double click a world orb and it opens the context menu
			Slot pressingSlot = touchInfo.source.Slot;
			InteractionHandler commonTool = pressingSlot.FindInteractionHandler();
			User user = commonTool.Owner;
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					Debug("World orb opened, clear previous pages");
					PreviousMenus.Clear();
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(InspectorMemberActions), "Pressed")]
	class InspectorMemberContextMenuPatch {
		public static bool Prefix(InspectorMemberActions __instance, IButton button, ButtonEventData eventData) { // This fires when you double click a world orb and it opens the context menu
			Slot pressingSlot = eventData.source.Slot;
			InteractionHandler commonTool = pressingSlot.FindInteractionHandler();
			User user = commonTool.Owner;
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					Debug("Inspector member opened, clear previous pages");
					PreviousMenus.Clear();
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ContextMenuExtensions), "OpenContextMenu")]
	class ContextMenuOpenAnyPatch {
		public static async void Postfix(User user, IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions)) { // This one fires for ANY context menu, including custom ones. Does not fire for root menus.
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					Debug("Any menu was opened");
					ContextMenu menu = user.GetUserContextMenu();
					if (menu != null) {
						await HandleButtonAfterAnimation(menu, user, summoner, pointer, options);
					}
				} else {
					TryFancyButton(user.GetUserContextMenu());
				}
			}
		}
	}

	[HarmonyPatch(typeof(ContextMenuSubmenu), "Pressed")]
	class SubMenuPressPatch {
		public static bool Prefix(ContextMenuSubmenu __instance, IButton button, ButtonEventData eventData) { // This fires when you click on a submenu.
			if (Config!.GetValue(Enabled) == true) {
				ContextMenuSubmenu submenu = __instance;
				if (submenu.ItemsRoot.Target == null || !ContextMenuSubmenu.IsValidSource(submenu.ItemsRoot.Target)) {
					return true;
				}
				PreviousMenus.Insert(0, submenu.ItemsRoot.Target);
				Debug("There are " + PreviousMenus.Count + " pages to go back to.");
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ContextMenu), "OnCommonUpdate")]
	class ContextMenuUpdatePatch {
		public static void Postfix(ContextMenu __instance) { // For animating the fancy button properly.
			if (__instance.IsUnderLocalUser) {
				if (Config!.GetValue(Enabled) && Config!.GetValue(AlternateDesign)) {
					if (__instance.IsVisible) {
						bool updateRequired = false;
						if (__instance.Lerp != lastLerp) {
							lastLerp = __instance.Lerp;
							updateRequired = true;
						}
						float innerLerp = 0;
						if (__instance._innerLerp != null) {
							innerLerp = __instance._innerLerp.Value;
						}
						if (innerLerp != lastInnerLerp) {
							lastInnerLerp = innerLerp;
							updateRequired = true;
						}
						if (updateRequired) {
							UpdateFancyButtonVisuals(__instance);
						}
					}
				}
			}
		}
	}
}
