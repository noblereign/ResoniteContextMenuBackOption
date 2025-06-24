using System;
using System.Collections.Generic;

using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using ResoniteHotReloadLib;
using System.Xml.Linq;
using FrooxEngine.UIX;

namespace ContextMenuBackOption;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
//Mod code partially based on https://github.com/XDelta/ResoniteFish (because it's a very simple context menu option mod lol)
public class ContextMenuBackOption : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "ContextMenuBackOption";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/ResoniteContextMenuBackOption";
	const string harmonyId = "dog.glacier.ContextMenuBackOption";

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Enables the mod. Pretty self explanatory!", () => true);
	public static readonly ModConfigurationKey<bool> ShowOnBuiltIn = new("Show on built-in submenus", "Should the Back option show up on built-in submenus? (e.g. the Locomotion submenu)", () => false);
	public static readonly ModConfigurationKey<bool> ShowOnSingleItemMenus = new("Show on single item menus", "Should the Back option show up on single item menus? Turning this on may break some systems that rely on having only one button present (for example, certain context menu sliders).", () => false);

	public static ModConfiguration? Config;

	static List<Slot> PreviousMenus = new();

	static Type SUBMENU_TYPE = TypeHelper.FindType("FrooxEngine.ContextMenuSubmenu");
	static Type ITEM_SOURCE_TYPE = TypeHelper.FindType("FrooxEngine.ContextMenuItemSource");
	static Type INTERACTION_HANDLER_TYPE = TypeHelper.FindType("FrooxEngine.InteractionHandler");

	public override void OnEngineInit() {
		HotReloader.RegisterForHotReload(this);

		Config = GetConfiguration()!;
		Config!.Save(true);

		// Call setup method
		Setup();
	}

	// This is the method that should be used to unload your mod
	// This means removing patches, clearing memory that may be in use etc.
	static void BeforeHotReload() {
		// Unpatch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.UnpatchAll(harmonyId);

		// clear any memory that is being used
		PreviousMenus.Clear();
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

	static void Setup() {
		// Patch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.PatchAll();
	}

	static void RemovePreviousPage(IButton button, ButtonEventData eventData) {
		Msg("Clicked, removing entries...");
		PreviousMenus.RemoveRange(0, 2);
	}

	[HarmonyPatch(typeof(InteractionHandler), "OpenContextMenu")]
	class ContextMenuOpenRootPatch {
		public static bool Prefix(InteractionHandler __instance, InteractionHandler.MenuOptions options) { // This one fires for Context Menu Root as well as the built-in ones (e.g. Locomotion, Grab Type)
			if (options == InteractionHandler.MenuOptions.Default || (Config!.GetValue(ShowOnBuiltIn) == false)) {
				Msg("Context menu root opened, clear previous menus");
				PreviousMenus.Clear();
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ContextMenuExtensions), "OpenContextMenu")]
	class ContextMenuOpenAnyPatch {
		public static async void Postfix(User user, IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions)) { // This one fires for ANY context menu, including custom ones.
			if (user.IsLocalUser) {
				if (Config!.GetValue(Enabled) == true) {
					Msg("Any menu was opened");
					ContextMenu menu = user.GetUserContextMenu();
					if (menu != null) {
						while ((menu._lerp.Value > 0f) && menu != null) {
							await default(NextUpdate);
						}
						//TODO: Options will tell if hidden, should we save hiddens?
						if (menu != null) {
							Slot itemRoot = menu._itemsRoot.Target;
							if (itemRoot.ChildrenCount <= 1) {
								Msg("Single button menu!");
								if (!(Config!.GetValue(ShowOnSingleItemMenus))) {
									return;
								}
							}

							Msg(PreviousMenus.Count);
							if (PreviousMenus.Count > 0) {
								IButton? Button = null;

								if (PreviousMenus.Count > 1) { // As far as I know you can't link back to the root menu in vanilla game... i think...
									foreach (Slot child in PreviousMenus[0].Children) {
										ContextMenuSubmenu? submenu = null;
										ContextMenuItemSource? source = null;
										foreach (IComponent component in child.Components) {
											if (component.GetType() == SUBMENU_TYPE) {
												submenu = (ContextMenuSubmenu)component;
											} else if (component.GetType() == ITEM_SOURCE_TYPE) {
												source = (ContextMenuItemSource)component;
											}
											if ((submenu != null) && (source != null)) {
												break;
											}
										}

										if (submenu != null && source != null && (submenu.ItemsRoot.Target == PreviousMenus[1])) {
											Msg("Existing back button found, hooking!");
											Button = source;
											break;
										}
									}
								}

								Msg("Made it past");
								if (Button == null) {
									Msg("Making own back button...");
									ContextMenuItem MenuItem = menu.AddItem("Back (Modded)", new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png"), colorX.White);

									Slot MenuSlot = MenuItem.Slot;
									MenuSlot.Tag = "BackOption";
									MenuSlot.OrderOffset = -10000;

									Button = MenuItem.Button;

									if (PreviousMenus.Count > 1) {
										ButtonPressEventRelay BackRelay = MenuSlot.AttachComponent<ButtonPressEventRelay>();
										BackRelay.Target.Value = PreviousMenus[1].ReferenceID;
									}
								}

								if (PreviousMenus.Count > 1) {
									Msg("Previous menu");
									Button.LocalPressed -= RemovePreviousPage; // C# why do i need this
									Button.LocalPressed += RemovePreviousPage;
								} else {
									Msg("Root menu");
									Button.LocalPressed += (IButton button, ButtonEventData eventData) => {
										Msg("Going back to root!");
										if (summoner.GetType() == INTERACTION_HANDLER_TYPE) {
											((InteractionHandler)summoner).OpenContextMenu(InteractionHandler.MenuOptions.Default);
										} else {
											Warn("Summoner was not interaction handler, falling back to primary hand");
											user.GetInteractionHandler(user.Primaryhand).OpenContextMenu(InteractionHandler.MenuOptions.Default);
										}
									};
								}
							}
						}
					}
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
				Msg("There are " + PreviousMenus.Count + " pages to go back to.");
				//Msg(__instance);
				/*if (options != InteractionHandler.MenuOptions.Default) {
					ContextMenuItem Button = ctx.AddItem("Back", new Uri("resdb:///66a1939382fbc85ebbd3cc80b812b71bb00506c52ca94cced1d21e76fbe7ef1c.png"), colorX.White);

					Slot MenuSlot = Button.Slot;
					MenuSlot.Tag = "BackOption";
				}*/
			}
			return true;
		}
	}
}
