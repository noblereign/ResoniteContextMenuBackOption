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
	internal const string VERSION_CONSTANT = "2.2.0";
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
	public static readonly ModConfigurationKey<bool> ShowOnSingleItemMenus = new("Show on single item menus", "Should the Back option show up on single item menus?\n\nTurning this on may break some systems that rely on having only one button present (for example, certain context menu sliders).", () => false);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> AlternateDesign = new("Alternate Design", "Instead of adding a back option, adds a new button between the empty space in the context menu.\n\n<color=yellow>NOTE:</color> With this setting enabled, the mod will hide existing back buttons, so as to not have duplicates.", () => false);
	//Alternate design suggested by U-PearPaw

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> DebugLogging = new("Debug Logging", "Enables extra logging. Off by default since they clog up the logs, but they can be useful when diagnosing issues.", () => false);

	[AutoRegisterConfigKey]
	private static ModConfigurationKey<dummy> CUSTOMIZATION_SEPERATOR = new("CUSTOMIZATION_SEPERATOR", "<color=hero.cyan>Button Customization", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<Uri> ButtonIcon = new("Back Button Icon", "Uri for the back button icon.", () => new Uri(("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png")));
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<colorX> ButtonColor = new("Back Button Color", "Color for the back button.", () => colorX.White);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<string> ButtonText = new("Back Button Description", "Description for the back button.\n\nYou can use the placeholders <b><Back></b> and <b><PreviousMenu></b> to get the localized 'Back' text and previous menu name respectively.\n\n<i>Applies to standard design only.</i>", () => new string("<Back>\n<size=50%><PreviousMenu></size>"));
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<long> ButtonOrder = new("Back Button Order", "Order offset for the back button.\nDefault: -10000\n\n<i>Applies to standard design only.</i>", () => -10000);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> ButtonSize = new("Back Button Size", "Size of the back button.\nDefault: 80\n\n<i>Applies to alternate design only.</i>", () => 80f);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<float> ButtonShrunkenSize = new("Back Button Small Size", "Size of the back button when shrunken.\nDefault: 40\n\n<i>Applies to alternate design only.</i>", () => 40f);

	[AutoRegisterConfigKey]
	private static ModConfigurationKey<dummy> OVERRIDE_SEPERATOR = new("OVERRIDE_SEPERATOR", "<color=hero.cyan>Existing Button Overrides", () => new dummy());

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> OverrideExistingIcons = new("Override Icon", "When a back button already exists, should its icon be replaced with the modded one?", () => false);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> OverrideExistingColors = new("Override Color", "When a back button already exists, should its color be replaced with the modded one?", () => false);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> OverrideExistingDescs = new("Override Description", "When a back button already exists, should its description be replaced with the modded one?", () => false);
	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> OverrideExistingOffset = new("Override Order", "When a back button already exists, should its order offset be replaced with the modded one?", () => false);
	

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

	static void DebugIfEnabled(object message) {
		if (Config!.GetValue(DebugLogging)) {
			Debug(message);
		}
	}

	static void RemovePreviousPage(IButton button, ButtonEventData eventData) {
		DebugIfEnabled("Clicked, removing entries...");
		int removed = 0;
		while (removed < 1) {
			if (PreviousMenus.Count <= 0) {
				break;
			}
			PreviousMenus.RemoveAt(0);
			removed++;
			DebugIfEnabled(removed + "removed");
		}
		DebugIfEnabled("==================================================");
		PreviousMenus.ForEach(item => DebugIfEnabled(item.Name));
		DebugIfEnabled("==================================================");
	}

	public static string TrimDescription(string description) {
		if (description.Length <= 32) {
			return description;
		}

		return description.Substring(0, 29) + "...";
	}

	public static string GetBackButtonText(User user) {
		string template = Config != null ? Config.GetValue(ButtonText)! : "<Back>\n<size=50%><PreviousMenu></size>";

		bool previousIsRoot = false;
		if (PreviousMenus.Count > 1) {
			previousIsRoot = (PreviousMenus[1] == user.GetUserContextMenu().Slot);
		} else {
			previousIsRoot = true;
		}

		string newText = template.Replace("<Back>", user.GetLocalized("General.Back"));

		if (previousIsRoot == true) {
			newText = newText.Replace("<PreviousMenu>", user.GetLocalized("Dash.Screens.Home"));
		} else {
			ContextMenuSubmenu? previousCtxSubmenu = PreviousMenus[1].GetComponent<ContextMenuSubmenu>();
			string previousMenuName = (previousCtxSubmenu != null && previousCtxSubmenu.ItemsRoot.Slot != null) ? previousCtxSubmenu.ItemsRoot.Slot.NameField.Value : PreviousMenus[1].NameField.Value;
			newText = newText.Replace("<PreviousMenu>", TrimDescription(previousMenuName));
		}

		return newText;
	}

	static void GeneralButtonHandler(User user, IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions)) {
		user.World.Coroutines.StartTask(async delegate
		{
			await default(NextUpdate);
			ContextMenu menu = user.GetUserContextMenu();
			if (menu != null) {
				DebugIfEnabled($"Trying for fancy button. Menu was found.");
				Tuple<Slot, Button>? FancyItems = TryFancyButton(menu);
				Slot? FancyButton = null;
				IButton? Button = null;
				DebugIfEnabled("Tried!");

				if (FancyItems != null) {
					DebugIfEnabled("Fancy button path");
					FancyButton = FancyItems.Item1;
					Button = FancyItems.Item2;
				}

				Slot itemRoot = menu._itemsRoot.Target;
				if (itemRoot.ChildrenCount <= 1) {
					DebugIfEnabled("Single button menu!");
					if (!(Config!.GetValue(ShowOnSingleItemMenus))) {
						if (FancyButton != null) {
							FancyButton.ActiveSelf = false;
						}
						return;
					}
				}

				if (FancyButton != null) {
					FancyButton.ActiveSelf = (PreviousMenus.Count > 0);
				}
				DebugIfEnabled("Fancy button active checked");
				if (PreviousMenus.Count > 0) {
					IButton? existingButton = null;
					Slot? existingContextMenuButton = null;
					if (PreviousMenus.Count > 1) { // As far as I know you can't link back to the root menu in vanilla game... i think...
						DebugIfEnabled("~~~~~~~~~~~~~~~~EXISTING BACK BUTTON CHECK!~~~~~~~~~~~~~~~~");
						ContextMenuSubmenu? currentCtxSubmenu = PreviousMenus[0].GetComponent<ContextMenuSubmenu>();
						Slot CheckOrigin = currentCtxSubmenu != null ? (currentCtxSubmenu.ItemsRoot.Target ?? PreviousMenus[0]) : PreviousMenus[0];

						ContextMenuSubmenu? previousCtxSubmenu = PreviousMenus[1].GetComponent<ContextMenuSubmenu>();
						Slot CheckTarget = previousCtxSubmenu != null ? (previousCtxSubmenu.ItemsRoot.Target ?? PreviousMenus[1]) : PreviousMenus[1];

						DebugIfEnabled($"Checking {CheckOrigin.Name} for menus pointing to {CheckTarget.Name}");
						foreach (Slot child in CheckOrigin.Children) {
							ContextMenuSubmenu? submenu = child.GetComponent<ContextMenuSubmenu>();
							ContextMenuItemSource? source = child.GetComponent<ContextMenuItemSource>();

							if (submenu != null && source != null) {
								DebugIfEnabled($"{child.Name} --> {(submenu.ItemsRoot.Target != null ? submenu.ItemsRoot.Target.Name : "<NO TARGET>")}");
								if (submenu.ItemsRoot.Target == CheckTarget) {
									DebugIfEnabled("!!!Existing back button found, hooking!!!");
									existingButton = source;
									break;
								}
							} else {
								DebugIfEnabled($"{child.Name} -/> Not a submenu");
							}
						}
						DebugIfEnabled("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
						if (existingButton != null) {

							DebugIfEnabled("Finding arc layout");
							Slot? ArcLayout = menu.Slot.FindChild("ArcLayout", false, false, 3);
							RefID existingRef = existingButton.Slot.ReferenceID;
							DebugIfEnabled(existingRef);
							if (ArcLayout != null) {
								DebugIfEnabled("Seearching ctx");
								foreach (Slot child in ArcLayout.Children) {
									ButtonPressEventRelay? eventRelay = child.GetComponent<ButtonPressEventRelay>();

									if (eventRelay != null && (eventRelay.Target.Value == existingRef)) {
										DebugIfEnabled("Existing back button slot found!! :D");
										existingContextMenuButton = eventRelay.Slot;
										break;
									}
								}
							}
						}
					}


					DebugIfEnabled("Checked for back buttons");

					bool previousIsRoot = false;
					if (PreviousMenus.Count > 1) {
						previousIsRoot = (PreviousMenus[1] == user.GetUserContextMenu().Slot);
					} else {
						previousIsRoot = true;
					}

					if (Button == null && existingButton == null) {
						DebugIfEnabled("Making own back button...");

						ContextMenuItem MenuItem = menu.AddItem(GetBackButtonText(user), (Config != null ? Config.GetValue(ButtonIcon)! : new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png")), (Config != null ? Config.GetValue(ButtonColor)! : colorX.White));
						Slot MenuSlot = MenuItem.Slot;
						MenuSlot.Tag = "BackOption";
						MenuSlot.OrderOffset = Config != null ? Config.GetValue(ButtonOrder)! : -10000;

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
									DebugIfEnabled("using existing relay");
									BackRelay.Target.Value = existingButton.Slot.ReferenceID;
									if (existingContextMenuButton != null) {
										existingContextMenuButton.ActiveSelf = false;
										DebugIfEnabled("Disabled existing back button.");
									} else {
										DebugIfEnabled("?! Existing back button not found ?!");
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

							if (Config!.GetValue(OverrideExistingIcons) == true) {
								DebugIfEnabled("Finding existing button icon slot.");
								Slot IconSlot = existingContextMenuButton.FindChild("Image");
								if (IconSlot != null) {
									DebugIfEnabled("Finding existing button image component.");
									Image ImageComponent = IconSlot.GetComponent<Image>();
									if (ImageComponent != null) {
										DebugIfEnabled("Forcing sprite target to modded.");

										// feel like this is a little bit dirty but ah well it works
										SpriteProvider spriteProvider = IconSlot.AttachComponent<SpriteProvider>();
										StaticTexture2D texture = IconSlot.AttachComponent<StaticTexture2D>();
										texture.URL.Value = (Config != null ? Config.GetValue(ButtonIcon)! : new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png"));
										spriteProvider.Texture.Target = texture;

										if (ImageComponent.Sprite.ActiveLink != null) {
											ImageComponent.Sprite.ActiveLink.ReleaseLink(undoable: false);
										}
										ImageComponent.Sprite.SetTarget(spriteProvider);
									}
								}
							}
							if (Config!.GetValue(OverrideExistingColors) == true) {
								DebugIfEnabled("Finding existing button data.");
								ContextMenuItem ItemComponent = existingContextMenuButton.GetComponent<ContextMenuItem>();
								if (ItemComponent != null) {
									DebugIfEnabled("Forcing item color change.");
									if (ItemComponent.Color.ActiveLink != null) {
										ItemComponent.Color.ActiveLink.ReleaseLink(undoable: false);
									}
									ItemComponent.Color.Value = (Config != null ? Config.GetValue(ButtonColor)! : colorX.White);
								}
							}
							if (Config!.GetValue(OverrideExistingDescs) == true) {
								DebugIfEnabled("Finding existing button text slot.");
								Slot TextSlot = existingContextMenuButton.FindChild("Text");
								if (TextSlot != null) {
									DebugIfEnabled("Finding existing button text component.");
									Text TextComponent = TextSlot.GetComponent<Text>();
									if (TextComponent != null) {
										DebugIfEnabled("Renaming existing context menu button.");
										if (TextComponent.Content.ActiveLink != null) {
											TextComponent.Content.ActiveLink.ReleaseLink(undoable: false);
										}
										TextComponent.Content.Value = GetBackButtonText(user);
									}
								}
							}
							if (Config!.GetValue(OverrideExistingOffset) == true) {
								existingContextMenuButton.OrderOffset = Config != null ? Config.GetValue(ButtonOrder)! : -10000;
							}
						}
					}

					if (Button != null) {
						Button.LocalPressed -= RemovePreviousPage; // C# why do i need this
						if ((PreviousMenus.Count > 1) && !previousIsRoot) {
							DebugIfEnabled("Previous menu");
							Button.LocalPressed += RemovePreviousPage;
							DebugIfEnabled("Added");
						} else {
							DebugIfEnabled("Root menu");
							Button.LocalPressed += (IButton button, ButtonEventData eventData) => {
								if (previousIsRoot) {
									RemovePreviousPage(button, eventData);
								}
								DebugIfEnabled("Going back to root!");
								if (summoner.GetType() == INTERACTION_HANDLER_TYPE) {
									((InteractionHandler)summoner).OpenContextMenu(InteractionHandler.MenuOptions.Default);
								} else {
									Warn("Summoner was not interaction handler, falling back to primary hand");
									user.GetInteractionHandler(user.Primaryhand).OpenContextMenu(InteractionHandler.MenuOptions.Default);
								}
							};
							DebugIfEnabled("Root menu fully added");
						}
					}
				}
			}
		});
	}

	public static async Task HandleButtonAfterAnimation(ContextMenu menu, User user, IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions)) {
		DebugIfEnabled("Waiting...");
		while ((menu._lerp.Value > 0f) && menu != null) {
			await default(NextUpdate);
		}
		if (menu != null) {
			DebugIfEnabled("Button will now be handled.");
			GeneralButtonHandler(user, summoner, pointer, options);
		} else {
			Warn("Cannot handle button, menu became null!");
		}
	}

	static Button SetupFancyButtonComponent(Slot FancyButton, Image image, OutlinedArc outlinedArc, DynamicReferenceVariable<Button> dynVar) {
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

		colorX color = (Config != null ? Config.GetValue(ButtonColor)! : colorX.White);
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
		colorDriver2.ColorDrive.ForceLink(outlinedArc.OutlineColor);

		dynVar.Reference.Value = button.ReferenceID;

		Slot FancyButtonIcon = FancyButton.FindChild("Icon");
		StaticTexture2D texture = FancyButtonIcon.GetComponent<StaticTexture2D>();
		texture.URL.Value = (Config != null ? Config.GetValue(ButtonIcon)! : new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png"));

		return button;
	}

	static Slot ConstructFancyButton(Slot RadialMenu) {
		Slot FancyButton = RadialMenu.AddSlot("CtxMenuBack");
		FancyButton.Tag = "BackOption";

		RectTransform rectTransform = FancyButton.AttachComponent<RectTransform>();
		rectTransform.AnchorMin.Value = new float2(0.25f, 0.25f);
		rectTransform.AnchorMax.Value = new float2(0.75f, 0.75f);
		OutlinedArc outlinedArc = FancyButton.AttachComponent<OutlinedArc>();

		UI_CircleSegment arcMaterial = RadialMenu.Parent.Parent.GetComponent<UI_CircleSegment>();

		float buttonArcSize = Config != null ? Config.GetValue(ButtonSize)! : 80f;

		outlinedArc.Arc.Value = buttonArcSize;
		outlinedArc.Offset.Value = 230f - ((buttonArcSize-80f)/2);
		outlinedArc.InnerRadiusRatio.Value = 0.6f;
		outlinedArc.OuterRadiusRatio.Value = 1f;
		outlinedArc.RoundedCornerRadius.Value = 14f;
		outlinedArc.OutlineThickness.Value = 2f;
		outlinedArc.Material.Target = arcMaterial;

		Slot ButtonIconSlot = FancyButton.AddSlot("Icon");
		RectTransform rectTransform2 = ButtonIconSlot.AttachComponent<RectTransform>();
		rectTransform2.AnchorMin.Value = new float2(0f, 0.025f);
		rectTransform2.AnchorMax.Value = new float2(1f, 0.175f);

		UI_UnlitMaterial imageMaterial = RadialMenu.Parent.Parent.GetComponent<UI_UnlitMaterial>();

		SpriteProvider spriteProvider = ButtonIconSlot.AttachComponent<SpriteProvider>();
		StaticTexture2D texture = ButtonIconSlot.AttachComponent<StaticTexture2D>();
		texture.URL.Value = (Config != null ? Config.GetValue(ButtonIcon)! : new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png"));
		spriteProvider.Texture.Target = texture;

		Image image = ButtonIconSlot.AttachComponent<Image>();
		image.Sprite.Target = spriteProvider;
		image.Material.Target = imageMaterial;

		DynamicReferenceVariable<Button> dynVar = FancyButton.AttachComponent<DynamicReferenceVariable<Button>>();
		dynVar.VariableName.Value = "User/ContextMenuBackOption.FancyButton";

		SetupFancyButtonComponent(FancyButton, image, outlinedArc, dynVar);
		return FancyButton;
	}

	private static Tuple<Slot, Button>? TryFancyButton(ContextMenu menu) {
		Slot? RadialMenu = menu.Slot.FindChild("Radial Menu", false, false, 2);
		if (RadialMenu != null) {
			DebugIfEnabled("Found radial, now look for button");
			Slot? FancyButton = RadialMenu.FindChild("CtxMenuBack", false, false, 1);
			Button? buttonComponent = null;
			DebugIfEnabled($"Fancy Button Found: {FancyButton != null}");
			if (FancyButton == null) { // There's probably a way to do this 100,000,000x better but I dunno how so :3
				DebugIfEnabled("No button found");
				if (Config!.GetValue(Enabled) == true && Config!.GetValue(AlternateDesign) == true) {
					// Manually construct the UI (horrible)
					FancyButton = ConstructFancyButton(RadialMenu);
					buttonComponent = FancyButton.GetComponent<Button>();
					DebugIfEnabled("Made fancy button");
				} else {
					return null;
				}
			} else if (Config!.GetValue(Enabled) == true && Config!.GetValue(AlternateDesign) == true) {
				DebugIfEnabled("Existing button found, look for icon");
				Slot? FancyButtonImage = FancyButton.FindChild("Icon", false, false, 1);
				if (FancyButtonImage != null) {
					DebugIfEnabled("we found the icon");
					buttonComponent = FancyButton.GetComponent<Button>();
					DebugIfEnabled($"Button component found: {buttonComponent != null}");
					if (buttonComponent != null) { // kinda hacky and i wish i didn't need to: re-create the button to make sure the LocalPressed events are always cleared
						DebugIfEnabled("Destorying button </3");
						try {
							buttonComponent.Destroy();
						} catch (Exception e) {
							Warn(e);
						}
					}
					DebugIfEnabled("btutton gone");
					Image fancyImage = FancyButtonImage.GetComponent<Image>();
					OutlinedArc fancyArc = FancyButton.GetComponent<OutlinedArc>();
					DynamicReferenceVariable<Button> dynVar = FancyButton.GetComponent<DynamicReferenceVariable<Button>>();

					DebugIfEnabled("Releasing links");
					if (fancyImage.Tint.ActiveLink != null) {
						fancyImage.Tint.ReleaseLink(fancyImage.Tint.ActiveLink);
					}
					if (fancyArc.FillColor.ActiveLink != null) {
						fancyArc.FillColor.ReleaseLink(fancyArc.FillColor.ActiveLink);
					}
					if (fancyArc.FillColor.ActiveLink != null) {
						fancyArc.FillColor.ReleaseLink(fancyArc.FillColor.ActiveLink);
					}
					DebugIfEnabled("Done, setting up fancy component");
					buttonComponent = SetupFancyButtonComponent(FancyButton, fancyImage, fancyArc, dynVar);
					DebugIfEnabled("ohhh yeahhh");
				} else {
					Warn("Could not find fancy button image, recreating the button entirely!");
					FancyButton.Destroy();
					FancyButton = ConstructFancyButton(RadialMenu);
					buttonComponent = FancyButton.GetComponent<Button>();
				}
			} else {
				DebugIfEnabled("Returning null");
				FancyButton?.Destroy();
				return null;
			}
			DebugIfEnabled("Returning tuple");
			return new Tuple<Slot, Button>(FancyButton, buttonComponent);
		}
		DebugIfEnabled("Truly null");
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

							float buttonArcSize = Config != null ? Config.GetValue(ButtonSize)! : 80f;
							float buttonSmallSize = Config != null ? Config.GetValue(ButtonShrunkenSize)! : 40f;

							float buttonArcOffset = 230f - ((buttonArcSize - 80f) / 2);
							float buttonArcSmallOffset = 250f - ((buttonSmallSize - 40f) / 2);

							if (innerLerp >= 0) {
								fancyArc.InnerRadiusRatio.Value = MathX.Remap(ctx.Lerp, 0f, 1f, .675f, .6f) + MathX.Remap(innerLerp, 0f, 1f, 0f, .2f);
								fancyArc.OuterRadiusRatio.Value = MathX.Remap(ctx.Lerp, 0f, 1f, .925f, 1f);
								fancyArc.RoundedCornerRadius.Value = MathX.Remap(innerLerp, 0f, 1f, 14f, 8f);
								fancyArc.Arc.Value = MathX.Remap(innerLerp, 0f, 1f, buttonArcSize, buttonSmallSize);
								fancyArc.Offset.Value = MathX.Remap(innerLerp, 0f, 1f, buttonArcOffset, buttonArcSmallOffset);
								fancyIconTransform.AnchorMin.Value = new float2(0f, 0.025f);
								fancyIconTransform.AnchorMax.Value = new float2(MathX.Remap(innerLerp, 0f, 1f, 1f, 1f), MathX.Remap(innerLerp, 0f, 1f, 0.175f, 0.075f));
							} else {
								fancyArc.InnerRadiusRatio.Value = .6f;
								fancyArc.OuterRadiusRatio.Value = 1f;
								fancyArc.RoundedCornerRadius.Value = 14f;
								fancyArc.Arc.Value = MathX.Remap(innerLerp, -1f, 0f, 0f, buttonArcSize);
								fancyArc.Offset.Value = MathX.Remap(innerLerp, -1f, 0f, buttonArcOffset + 40f, buttonArcOffset);
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
					DebugIfEnabled("Context menu root opened, clear previous menus");
					PreviousMenus.Clear();
				} else if (Config!.GetValue(ShowOnBuiltIn)) {
					DebugIfEnabled("Built in context menu opened, add root back");
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
				DebugIfEnabled("Attempting button handler.");
				await __instance.World.Coroutines.StartTask(async delegate
				{
					await HandleButtonAfterAnimation(__instance.LocalUser.GetUserContextMenu(), __instance.LocalUser, __instance, __instance.PointReference, default(ContextMenuOptions));
				}); 
				DebugIfEnabled("Button handled.");
			}
		}
	}

	[HarmonyPatch(typeof(ContextMenuExtensions), "ContextMenuConfirm")]
	class ContextMenuConfirmActionPatch {
		public static bool Prefix(User user, IWorldElement summoner, Slot pointer, LocaleString actionName, Uri actionIcon, colorX actionColor, ButtonEventHandler actionCallback, bool hidden = false) { // This fires for actions that require confirmation (e.g. entering an anchor, equipping an avatar/tool...)
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					DebugIfEnabled("Confirmation opened, clear previous pages");
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
					DebugIfEnabled("World orb opened, clear previous pages");
					PreviousMenus.Clear();
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(InspectorMemberActions), "Pressed")]
	class InspectorMemberContextMenuPatch {
		public static bool Prefix(InspectorMemberActions __instance, IButton button, ButtonEventData eventData) { // This fires when you open the inspector member context menu
			Slot pressingSlot = eventData.source.Slot;
			InteractionHandler commonTool = pressingSlot.FindInteractionHandler();
			User user = commonTool.Owner;
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					DebugIfEnabled("Inspector member opened, clear previous pages");
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
					DebugIfEnabled("Any menu was opened");
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
				DebugIfEnabled(button.Slot.Tag);
				if ((button != null && button.Slot != null && button.Slot.Tag == "BackOption") || submenu.Slot == null || !ContextMenuSubmenu.IsValidSource(submenu.Slot) || submenu.ItemsRoot.Target == null || !ContextMenuSubmenu.IsValidSource(submenu.ItemsRoot.Target)) {
					return true;
				}
				PreviousMenus.Insert(0, submenu.Slot);
				DebugIfEnabled("There are " + PreviousMenus.Count + " pages to go back to.");
				DebugIfEnabled("==================================================");
				PreviousMenus.ForEach(item => DebugIfEnabled(item.Name));
				DebugIfEnabled("==================================================");
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
