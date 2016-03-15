﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace xlflib
{
    public class XlfDocument
    {
        private XDocument doc;
        public XlfDocument(string fileName)
        {
            FileName = fileName;
            doc = XDocument.Load(FileName);
        }

        public string FileName
        { get; }

        public string Version
        { get { return this.doc.Root.Attribute("version").Value; } }

        public List<XlfFile> Files
        {
            get
            {
                var ns = this.doc.Root.Name.Namespace;
                return new List<XlfFile>(this.doc.Descendants(ns + "file").Select(f => new XlfFile(f)));
            }
        }

        public void Save()
        {
            this.doc.Save(this.FileName);
        }

        public enum SaveMode
        {
            Default, Sorted
        }

        public void SaveAsResX(string fileName)
        {
            SaveAsResX(fileName, SaveMode.Default);
        }

        public void SaveAsResX(string fileName, SaveMode mode)
        {
            using (ResXResourceWriter resx = new ResXResourceWriter(fileName))
            {
                var nodes = new List<ResXDataNode>();
                foreach (var f in Files)
                {
                    foreach (var u in f.TransUnits)
                    {
                        var id = u.Id;
                        if (u.Optional.Resname.Length > 0)
                        {
                            id = u.Optional.Resname;
                        }
                        else if (id.Length > 5 && id.Substring(0, 5).ToUpperInvariant() == "RESX/")
                        {
                            id = id.Substring(5);
                        }

                        var node = new ResXDataNode(id, u.Target);
                        if (u.Optional.Notes.Count > 0)
                        {
                            node.Comment = u.Optional.Notes.First();
                        }
                        nodes.Add(node);
                    }
                }

                if (mode == SaveMode.Sorted)
                {
                    nodes.Sort((x, y) =>
                    {
                        if (x.Name == null && y.Name == null) return 0;
                        else if (x.Name == null) return -1;
                        else if (y.Name == null) return 1;
                        else return x.Name.CompareTo(y.Name);
                    });
                }

                foreach (var node in nodes)
                {
                    resx.AddResource(node);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Number of updated and number of added items</returns>
        public Tuple<int, int> UpdateFromResX(string fileName)
        {
            var resxData = new Dictionary<string, Tuple<string, string>>(); // name, data, comment
            using (var resx = new ResXResourceReader(fileName))
            {
                resx.UseResXDataNodes = true;
                var dict = resx.GetEnumerator();
                while (dict.MoveNext())
                {
                    var x = dict.Value as ResXDataNode;
                    resxData.Add(
                        dict.Key as string,
                        Tuple.Create(
                            x.GetValue((ITypeResolutionService)null) as string,
                            x.Comment));
                }
            }

            int updatedItems = 0;
            int addedItems = 0;
            foreach (var f in Files)
            {
                foreach (var u in f.TransUnits)
                {
                    var key = u.Optional.Resname.Length > 0 ? u.Optional.Resname : u.Id;
                    if (resxData.ContainsKey(key) && u.Source != resxData[key].Item1)
                    {
                        // source text changed
                        u.Source = resxData[key].Item1;
                        u.Optional.TargetState = "new";
                        ++updatedItems;
                    }
                    resxData.Remove(key);
                }

                foreach (var d in resxData)
                {
                    var unit = f.AddTransUnit(d.Key, d.Value.Item1, d.Value.Item2);
                    unit.Optional.TargetState = "new";
                    ++addedItems;
                }
            }

            return Tuple.Create(updatedItems, addedItems);
        }
    }
}