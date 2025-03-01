using ReClassNET.Plugins;
using ReClassNET.Logger;
using ReClassNET.Nodes;
using ReClassNET.Memory;
using System;
using HashLookupPlugin;
using System.Diagnostics.Contracts;
using System.Windows.Forms;

using System.Collections.Generic;
using System.Drawing;
using ReClassNET.Controls;
using ReClassNET.UI;
using ReClassNET.Extensions;
using System.Xml.Linq;
using System.Diagnostics;
using ReClassNET.DataExchange.ReClass;
using System.Runtime.Remoting.Contexts;
using System.Linq;
using System.Security.Policy;
using System.Data.OleDb;
using System.Threading;
using ReClassNET.Forms;
using ReClassNET;

namespace HashLookupPlugin
{
    public class HashLookupPluginExt : Plugin
    {
        private IPluginHost host;
        public static Dictionary<ulong, string> Hashes;
        public static bool useComments = true;

        static HashLookupPluginExt()
        {
            Hashes = HashDictionary.Hashes;
            System.Diagnostics.Debug.WriteLine("Loaded plugin.");
        }

        public override bool Initialize(IPluginHost host)
        {
            this.host = host;

            // Notfiy the plugin if a window is shown.
            GlobalWindowManager.WindowAdded += OnWindowAdded;

            return true;
        }

        public override void Terminate()
        {
            GlobalWindowManager.WindowAdded -= OnWindowAdded;

            host = null;
        }

        // <ParentNode, <oldNode, newNode>>
        public override Image Icon => Properties.Resources.logo_hash1;

        public override CustomNodeTypes GetCustomNodeTypes()
        {
            return new CustomNodeTypes
            {
                CodeGenerator = new StringHashCodeGenerator(),
                Serializer = new StringHashNodeConverter(),
                NodeTypes = new[] { typeof(StringHashNode) }
            };
        }


        public override IReadOnlyList<INodeInfoReader> GetNodeInfoReaders()
        {
            return new[] { new NodeInfoReader() };
        }

        private void OnWindowAdded(object sender, GlobalWindowManagerEventArgs e)
        {
            if (e.Form is SettingsForm settingsForm)
            {
                settingsForm.Shown += delegate (object sender2, EventArgs e2)
                {
                    try
                    {
                        var settingsTabControl = settingsForm.Controls.Find("settingsTabControl", true).FirstOrDefault() as TabControl;
                        if (settingsTabControl != null)
                        {
                            var newTab = new TabPage("Symbol parser")
                            {
                                UseVisualStyleBackColor = true
                            };

                            var checkBox = new CheckBox
                            {
                                AutoSize = true,
                                Text = "Use comments",
                                AutoCheck = true,
                                Checked = useComments
                            };
                            checkBox.CheckedChanged += delegate { useComments = checkBox.Checked; };
                            newTab.Controls.Add(checkBox);

                            settingsTabControl.TabPages.Add(newTab);
                        }
                    }
                    catch
                    {

                    }
                };
            }
        }
    }







    public class NodeInfoReader : INodeInfoReader
    {
        private Dictionary<BaseContainerNode, Dictionary<BaseNode, BaseNode>> toReplace = new Dictionary<BaseContainerNode, Dictionary<BaseNode, BaseNode>>();
        private BaseContainerNode lastParent;


        public void addToReplace(BaseNode oldNode, BaseNode newNode, BaseContainerNode parentNode)
        {
            //newNode.CopyFromNode(oldNode);
            if (toReplace.TryGetValue(parentNode, out Dictionary<BaseNode, BaseNode> nodesToReplace))
            {
                if (nodesToReplace.ContainsKey(oldNode))
                    return;
                nodesToReplace.Add(oldNode, newNode);
            }
            else
            {
                Debug.WriteLine($"Creating new parent dictionary under {parentNode.Name}");
                Dictionary<BaseNode, BaseNode> nodesToReplace2 = new Dictionary<BaseNode, BaseNode>();
                nodesToReplace2.Add(oldNode, newNode);
                toReplace.Add(parentNode, nodesToReplace2);
            }
            Debug.WriteLine($"{oldNode.Offset} | Buffered node {oldNode.Name}({oldNode.GetType().ToString()}) to be swapped with node {newNode.Name}({newNode.GetType().ToString()}) under {parentNode.Name}");
        }

        public bool swapNode(BaseNode oldNode, BaseNode newNode, BaseContainerNode parentNode)
        {
            if (!parentNode.Nodes.Contains(oldNode))
            {
                Debug.WriteLine($"Node {oldNode.Name} not found in {parentNode.Name}");
                return false;
            }
            Debug.WriteLine($"{oldNode.Offset} | Swapping {newNode.Name} with {oldNode.Name} in {parentNode.Name}");
            parentNode.InsertNode(oldNode, newNode);
            if (!parentNode.RemoveNode(oldNode))
            {
                Debug.WriteLine($"{oldNode.Offset} | Failed to remove node {oldNode.Name}");
                return false;
            }
            //parentNode.UpdateOffsets();
            return true;
        }

        public string ReadNodeInfo(BaseHexCommentNode node, IRemoteMemoryReader reader, MemoryBuffer memory, IntPtr nodeAddress, IntPtr nodeValue)
        {
            var parNode = node.GetParentContainer();
            if (parNode == null || lastParent == parNode)
                return "";
            //Debug.WriteLine($"Parent class: {parNode.Name} | Parent container: {node.GetParentContainer().Name}");

            if (toReplace.Count > 0)
            {
                for (int i = 0; i < toReplace.Count; i++)
                {
                    var item = toReplace.ElementAt(i);
                    if (item.Key != parNode)
                    {
                        foreach (var item1 in item.Value)
                        {
                            //if (parNode.Nodes[parNode.FindNodeIndex(item1.Key)].IsHidden)
                            if (item.Key.Nodes.Contains(item1.Key))
                            {
                                if (swapNode(item1.Key, item1.Value, item.Key))
                                    Debug.WriteLine($"Node {item1.Key.Name} swapped successfully");
                                else
                                    Debug.WriteLine($"Failed to swap node {item1.Key.Name}");
                            }
                            else
                            {
                                item.Value.Remove(item1.Key);
                                break;
                            }
                        }
                        if (item.Value.Count == 0)
                            if (!toReplace.Remove(item.Key))
                                Debug.WriteLine($"Failed to remove parent node \"{item.Key.Name}\" with {item.Value.Count} children");
                            else
                                Debug.WriteLine($"Successfully removed parent node \"{item.Key.Name}\"");
                    }
                }
            }


            for (int i = 0; i < parNode.Nodes.Count; i++)
            {
                var item = parNode.Nodes[i];
                var typee = item.GetType();
                //if (typee != typeof(Hex64Node) || typee != typeof(StringHashNode))
                //continue;
                bool isHash = HashLookupPluginExt.Hashes.TryGetValue(memory.ReadUInt64(item.Offset), out string str);
                bool isStringHashNode = typee == typeof(StringHashNode);
                StringHashNode itemAsStringHashNode = new StringHashNode();
                if (isStringHashNode)
                {
                    itemAsStringHashNode = (StringHashNode)item;
                    string oldStringHash = itemAsStringHashNode.hashedString;
                    if (oldStringHash != str)
                    {
                        Debug.WriteLine($"HashString \"{oldStringHash}\" changed to \"{str}\"");
                        var newNode = new StringHashNode();
                        newNode.hashedString = str;
                        newNode.CopyFromNode(item);
                        newNode.autoCreated = true;
                        addToReplace(item, newNode, parNode);
                        continue;
                    }
                }


                if (isHash && !isStringHashNode)
                {
                    if (typee != typeof(Hex64Node) || HashLookupPluginExt.useComments)
                    {
                        item.Comment = str;
                        continue;
                    }
                    item.Comment = "";
                    var newNode = new StringHashNode();
                    newNode.hashedString = str;
                    newNode.CopyFromNode(item);
                    newNode.autoCreated = true;
                    addToReplace(item, newNode, parNode);


                    //Debug.WriteLine($"1 Type: {pnewNode.ToString()} Name: {pnewNode.Name} Offset: {pnewNode.Offset}");
                    //Debug.WriteLine($"2 Type: {parNode.ToString()} Name: {parNode.Name} Offset: {parNode.Offset}\n");
                    //parNode.AddNode(newNode);
                    //newNode.Offset = item.Offset;

                    //item.Comment = $"  <SYMBOL> {str}";
                    //parNode.InsertNode(item, newNode);
                    //parNode.ReplaceChildNode(item, newNode);
                }
                else if (!isHash && (isStringHashNode || HashLookupPluginExt.useComments))
                {
                    if (isStringHashNode)
                    {
                        var newNode = new Hex64Node();
                        newNode.CopyFromNode(item);
                        addToReplace(item, newNode, parNode);
                    }
                    if (item.Comment != "")
                    {
                        item.Comment = "";
                    }
                }
                else if (isStringHashNode)
                {
                    if (HashLookupPluginExt.useComments && itemAsStringHashNode.autoCreated)
                    {
                        var newNode = new Hex64Node();
                        newNode.CopyFromNode(item);
                        newNode.Comment = str;
                        addToReplace(item, newNode, parNode);
                        continue;
                    }
                    item.Comment = "";
                }
                /*
                if (HashLookupPluginExt.Hashes.TryGetValue(memory.ReadUInt64(item.Offset), out string str))
                {
                    item.Comment = $"  <SYMBOL> {str}";
                }
                else if(item.Comment != null)
                {
                    item.Comment = "";
                }
                */
            }

            lastParent = parNode;

            return "";
        }
    }

    public class StringHashNode : BaseHexNode
    {
        private readonly MemoryBuffer memory = new MemoryBuffer();

        public bool autoCreated;

        public override int MemorySize => sizeof(ulong);

        public string hashedString;

        public override void GetUserInterfaceInfo(out string name, out Image icon)
        {
            name = "Hashed String";
            icon = Properties.Resources.logo_hash1; // Add an icon if needed
        }

        public StringHashNode()
        {
        }

        public override void Initialize()
        {
        }

        public override Size Draw(DrawContext context, int x, int y)
        {
            if (IsHidden && !IsWrapped)
            {
                return DrawHidden(context, x, y);
            }

            var val = context.Memory.ReadUInt64(Offset);
            if (HashLookupPluginExt.Hashes.TryGetValue(val, out string str))
            {
                hashedString = str;
            }
            else
            {
                hashedString = $"Not a symbol";
            }
            var origX = x;
            var origY = y;


            AddSelection(context, x, y, context.Font.Height);

            /*
            x = AddIconPadding(context, x);
            x = AddIconPadding(context, x);

            var tx = x;

            x = AddAddressOffset(context, x, y);
            */

            var newsize = base.Draw(context, x, y, context.Settings.ShowNodeText ? context.Memory.ReadString(context.Settings.RawDataEncoding, Offset, 8) + " " : null, 8);
            x += newsize.Width;
            origY -= newsize.Height;

            x = AddText(context, x, y, context.Settings.PluginColor, HotSpot.NoneId, $"<SYMBOL> {hashedString}");

            x += context.Font.Width;

            //AddComment(context, x, y);

            AddDeleteIcon(context, y);

            //y += context.Font.Height;

            var size = new Size(x - origX, y - origY);

            return size;
        }

        public override int CalculateDrawnHeight(DrawContext context)
        {
            if (IsHidden && !IsWrapped)
            {
                return HiddenHeight;
            }

            var h = context.Font.Height;
            return h;
        }
    }
}
