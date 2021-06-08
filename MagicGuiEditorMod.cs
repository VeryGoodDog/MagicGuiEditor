using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Microsoft.CSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace MagicGuiEditor {
	public class GuiReloaderMod : ModSystem {
		#region fieldsNstuff

		// saves some time
		private const int FLAGS = (int) (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
																		BindingFlags.DeclaredOnly);

		private ICoreClientAPI ClientAPI;
		private IClientEventAPI EventAPI;
		private ILogger Logger;

		private Config config;
		private FileSystemWatcher watcher;
		private CompilerParameters param;
		private CSharpCodeProvider provider;
		private bool compiling = false;

		private Type oldGuiType;

		private GuiDialog guiRef;

		// this is so gross i am sorry
		private static Type newGuiType;
		private MethodInfo[] methods;

		#endregion

		public override bool ShouldLoad(EnumAppSide forSide) => forSide.IsClient();

		// make sure it loads last
		public override double ExecuteOrder() => 1;

		public override void StartClientSide(ICoreClientAPI api) {
			ClientAPI = api;
			EventAPI = ClientAPI.Event;
			Logger = ClientAPI.Logger;
			LoadAndRefresh();

			ClientAPI.RegisterCommand("refreshmgem", "reload and refresh MGEM config", "",
				(id, args) => { LoadAndRefresh(); });
			ClientAPI.RegisterCommand("mgemconfig", "set MGEM config",
				".mgemconfig [pathname|namespacedTypeName|refs add|refs remove] <arg>", Handler);

			EventAPI.LevelFinalize += LoadTypes;
			EventAPI.LeaveWorld += SaveConfig;
		}

		private void Handler(int groupid, CmdArgs args) {
			var opt = args.PopWord();
			var next = args.PopWord();
			if (opt != null) {
				List<string> r;
				switch (opt) {
					case "pathname":
						if (next == null) {
							ClientAPI.ShowChatMessage(config.pathname);
							return;
						}
						if (!File.Exists(next)) {
							ClientAPI.ShowChatMessage("That file does not exist!");
							return;
						}
						config.pathname = next;
						watcher?.Dispose();
						StartWatcher();
						break;
					case "namespacedTypeName":
						if (next == null) {
							ClientAPI.ShowChatMessage(config.pathname);
							return;
						}
						config.namespacedTypeName = next;
						LoadTypes();
						break;
					case "refs":
						if (next == null) {
							foreach (var configRef in config.refs) {
								ClientAPI.ShowChatMessage(configRef);
							}
							return;
						}
						var nextNext = args.PopWord();
						if (next == "remove") {
							if (!config.refs.Contains(nextNext))
								break;
							r = config.refs.ToList();
							r.Remove(next);
							config.refs = r.ToArray();
						} else if (next == "add") {
							if (config.refs.Contains(nextNext))
								break;
							r = config.refs.ToList();
							r.Add(next);
							config.refs = r.ToArray();
						}
						InitCompiler();
						break;
				}
				SaveConfig();
			}
			ClientAPI.ShowChatMessage($"pathname: {config.pathname}");
			ClientAPI.ShowChatMessage($"namespacedTypeName: {config.namespacedTypeName}");
			ClientAPI.ShowChatMessage($"refs:");
			for (var i = 0; i < config.refs.Length; i++) {
				ClientAPI.ShowChatMessage($"\t{config.refs[i]}");
			}
		}

		// get all the references and types that are needed to recompile and redirect the methods
		private void LoadTypes() {
			if (config.namespacedTypeName == "")
				return;
			oldGuiType = AccessTools.TypeByName(config.namespacedTypeName);
			guiRef = ClientAPI.Gui.LoadedGuis.Find(dialog => dialog.GetType() == oldGuiType);
			methods = oldGuiType.GetMethods((BindingFlags) FLAGS);
		}

		private void LoadAndRefresh() {
			LoadConfig();
			StartWatcher();
			InitCompiler();
		}

		private void LoadConfig() {
			try {
				config = ClientAPI.LoadModConfig<Config>(Config.CONFIG_PATH);
			} catch (Exception) {
				Logger.Debug("Error loading GUI config.");
			}
			if (config == null)
				config = new Config();
		}

		private void StartWatcher() {
			if (config.pathname == "")
				return;
			watcher = new FileSystemWatcher() {
				Path = Path.GetDirectoryName(config.pathname),
				Filter = config.filename,
				EnableRaisingEvents = true
			};
			watcher.Changed += (sender, e) => {
				if (compiling)
					return;
				compiling = true;
				watcher.EnableRaisingEvents = false;
				CompilerShit();
				EventAPI.EnqueueMainThreadTask(handleChange, "gui recomp");
			};
			watcher.Error += (s, a) => Logger.Error("GUI file watcher reported an error!");
			Logger.Debug($"Now watching {config.pathname} for changes.");
		}

		private void handleChange() {
			guiRef.TryClose();
			DoPatches();
			watcher.EnableRaisingEvents = true;
			guiRef.TryOpen();
			compiling = false;
		}

		private void DoPatches() {
			if (newGuiType == null)
				return;
			for (var i = 0; i < methods.Length; i++) {
				var name = methods[i].Name;
				var method = newGuiType.GetMethod(name, (BindingFlags) FLAGS);
				// this does a thing that makes the new method be like the old method, its magic i dont know
				Memory.DetourMethod(methods[i], method);
			}
		}

		private void CompilerShit() {
			var result = provider.CompileAssemblyFromFile(param, config.pathname);
			if (result.Errors.HasErrors) {
				Logger.Error("MGEM encountered recompilation errors!");
				foreach (CompilerError error in result.Errors)
					Logger.Error($"Error {error.ErrorNumber}: {error.ErrorText}");
				newGuiType = null;
				return;
			}
			newGuiType = result.CompiledAssembly.GetType(config.namespacedTypeName);
		}

		private void InitCompiler() {
			param = new CompilerParameters() {
				GenerateExecutable = false,
				GenerateInMemory = true,
				ReferencedAssemblies = {
					"System",
					"VintagestoryLib.dll",
					"VintagestoryAPI.dll",
					"Mods/VSCreativeMod.dll",
					"Mods/VSSurvivalMod.dll",
					"Mods/VSEssentials.dll",
				},
			};
			// just add every lib, who gives a shit.
			foreach (var file in Directory.GetFiles("Lib")) {
				if (Path.HasExtension("dll"))
					param.ReferencedAssemblies.Add(file);
			}
			param.ReferencedAssemblies.AddRange(config.refs);
			provider = new CSharpCodeProvider();
		}

		private void SaveConfig() {
			ClientAPI.StoreModConfig(
				config,
				Config.CONFIG_PATH
			);
			Logger.Debug("Saved MGEM config.");
		}
	}
}