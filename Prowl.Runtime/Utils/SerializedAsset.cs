﻿using Prowl.Runtime.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;

namespace Prowl.Runtime.Utils
{
    public class SerializedAsset
    {
        public EngineObject? Main;
        public List<EngineObject> SubAssets = new();

        public bool HasMain => Main != null;

        public void SaveToFile(FileInfo file)
        {
            if (Main == null) throw new Exception("Asset does not have a main object.");

#warning TODO: Somehow record Dependencies - reflection to find all AssetRef instances and store them in a list?
            // We need a way for said Dependencies to be able to be loaded and references independently of the asset
            // so like if we delete another asset we can let the user know its in use by X object are they sure?

            file.Directory?.Create(); // Ensure the Directory exists
            var tag = Serializer.Serialize(this);
            using var stream = file.OpenWrite();
            using BinaryWriter writer = new(stream);
            BinaryTagConverter.WriteTo(tag, writer);
        }

        public void SaveToStream(Stream writer)
        {
            if (Main == null) throw new Exception("Asset does not have a main object.");

            var tag = Serializer.Serialize(this);
            using BinaryWriter binarywriter = new(writer);
            BinaryTagConverter.WriteTo(tag, binarywriter);
        }

        public static SerializedAsset FromSerializedAsset(string path)
        {
            using var stream = File.OpenRead(path);
            using BinaryReader reader = new(stream);
            var tag = BinaryTagConverter.ReadFrom(reader);

            bool prev = SceneManager.AllowGameObjectConstruction;
            SceneManager.AllowGameObjectConstruction = false;
            var obj = Serializer.Deserialize<SerializedAsset>(tag);
            SceneManager.AllowGameObjectConstruction = prev; // Restore state
            return obj;
        }

        public static SerializedAsset FromStream(Stream stream)
        {
            using BinaryReader reader = new(stream);
            var tag = BinaryTagConverter.ReadFrom(reader);

            bool prev = SceneManager.AllowGameObjectConstruction;
            SceneManager.AllowGameObjectConstruction = false;
            var obj = Serializer.Deserialize<SerializedAsset>(tag);
            SceneManager.AllowGameObjectConstruction = prev; // Restore state
            return obj;
        }

        public void AddSubObject(EngineObject obj)
        {
            if (obj == null) throw new Exception("Asset cannot be null");
            if (SubAssets.Contains(obj) || ReferenceEquals(Main, obj)) throw new Exception("Asset already contains this object: " + obj);
            obj.FileID = (short)SubAssets.Count;
            SubAssets.Add(obj);
        }

        public void SetMainObject(EngineObject obj)
        {
            if (obj == null) throw new Exception("Asset cannot be null");
            if (SubAssets.Contains(obj)) throw new Exception("Asset already contains this object: " + obj);
            obj.FileID = (short)0;
            Main = obj;
        }
    }
}
