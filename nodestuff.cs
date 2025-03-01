using System.Collections.Generic;
using System.Xml.Linq;
using ReClassNET.CodeGenerator;
using ReClassNET.DataExchange.ReClass;
using ReClassNET.Logger;
using ReClassNET.Nodes;

namespace HashLookupPlugin
{
    public class StringHashCodeGenerator : CustomCppCodeGenerator
    {
        public override bool CanHandle(BaseNode node)
        {
            return node is StringHashNode;
        }

        public override BaseNode TransformNode(BaseNode node)
        {
            return node;
        }

        public override string GetTypeDefinition(BaseNode node, GetTypeDefinitionFunc defaultGetTypeDefinitionFunc, ResolveWrappedTypeFunc defaultResolveWrappedTypeFunc, ILogger logger)
        {
            return $"SYMBOL";
        }
    }

    public class StringHashNodeConverter : ICustomNodeSerializer
    {
        private const string XmlType = "HashLookup::StringHash";
        public bool CanHandleNode(BaseNode node) => node is StringHashNode;
        public bool CanHandleElement(XElement element) => element.Attribute(ReClassNetFile.XmlTypeAttribute)?.Value == XmlType;

        /// <summary>Creates a node from the xml element. This method gets only called if <see cref="CanHandleElement(XElement)"/> returned true.</summary>
        /// <param name="element">The element to create the node from.</param>
        /// <param name="parent">The parent of the node.</param>
        /// <param name="classes">The list of classes which correspond to the node.</param>
        /// <param name="logger">The logger used to output messages.</param>
        /// <returns>True if a node was created, otherwise false.</returns>
        public BaseNode CreateNodeFromElement(XElement element, BaseNode parent, IEnumerable<ClassNode> classes, ILogger logger, CreateNodeFromElementHandler defaultHandler)
        {
            return new StringHashNode();
        }

        public XElement CreateElementFromNode(BaseNode node, ILogger logger, CreateElementFromNodeHandler defaultHandler)
        {
            return new XElement(
                ReClassNetFile.XmlNodeElement,
                new XAttribute(ReClassNetFile.XmlTypeAttribute, XmlType)
            );
        }
    }
}