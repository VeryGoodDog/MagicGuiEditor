# MagicGuiEditor
The MagicGuiEditor(Mod), MGEM, reloads GUIs as you update their code.

## Configuration
In order to recompile your GUI MGEM needs a few things:
- Path of the file, eg `aliases/GuiDialogAliasEditor.cs`.
- Namespaced type name, eg `CommandMacros.GuiDialogAliasEditor`.
- Assemblies needed to compile the file.
MGEM automatically includes every assembly in the game by default.
  
To set these, use the `.mgemconfig` command:
- `.mgemconfig`: Prints the current settings.
- `.mgemconfig pathname <path>`: Sets the path.
- `.mgemconfig namespacedTypeName <name>`: Sets the type name.
- `.mgemconfig refs add <assembly>`: Adds a reference assembly.
- `.mgemconfig refs remove <assembly>`: Removes an assembly.

## Current Limitations
MGEM cannot handle methods being deleted.

Additionally, because methods are only redirected,
properties set in the constructor are not updated.
This means any dialog bounds configured in the ctor will not update.
If you move the configuration to happen in the `GuiDialog.OnGuiOpened()` method
this issue is resolved.

These problems may be correctly resolved in the future.