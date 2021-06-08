using System.IO;
using Newtonsoft.Json;

namespace MagicGuiEditor {
	internal class Config {
		[JsonIgnore]
		public static readonly string CONFIG_PATH = "mgem.json";
		/// <summary>
		/// The path to the GUI's file, including filename.
		/// </summary>
		public string pathname = "";
		/// <summary>
		/// The namespaced type name of the GUI.
		/// </summary>
		public string namespacedTypeName = "";
		/// <summary>
		/// The GUI's filename.
		/// </summary>
		[JsonIgnore]
		public string filename => Path.GetFileName(pathname);
		/// <summary>
		/// Any additional references needed to compile the GUI class.
		/// </summary>
		public string[] refs = new string[0];
	}
}