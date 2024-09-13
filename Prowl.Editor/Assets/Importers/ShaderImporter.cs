﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Editor.Utilities;
using Prowl.Runtime;
using Prowl.Runtime.Utils;

namespace Prowl.Editor.Assets
{
    [Importer("ShaderIcon.png", typeof(Shader), ".shader")]
    public class ShaderImporter : ScriptedImporter
    {
        private static Shader s_internalError;

        public static readonly string[] Supported = { ".shader" };

        public override void Import(SerializedAsset ctx, FileInfo assetPath)
        {
            string shaderScript = File.ReadAllText(assetPath.FullName);

            string relPath = AssetDatabase.GetRelativePath(assetPath.FullName);
            relPath = relPath.Substring(relPath.IndexOf(Path.DirectorySeparatorChar));

            FileIncluder includer = new FileIncluder(relPath, [Project.Active.AssetDirectory, Project.Active.DefaultsDirectory, Project.Active.PackagesDirectory]);

            if (!ShaderParser.ParseShader(shaderScript, includer, out Shader? shader))
            {
                if (assetPath.Name == "InternalErrorShader.shader")
                {
                    Debug.LogError("InternalErrorShader failed to compile. Non-compiling shaders loaded through script will cause cascading exceptions.");
                    return;
                }

                if (s_internalError == null)
                    s_internalError = Application.AssetProvider.LoadAsset<Shader>("Defaults/InternalErrorShader.shader").Res!;

                shader = s_internalError;
            }

            ctx.SetMainObject(shader);
        }
    }
}
