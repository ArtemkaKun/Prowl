﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Prowl.Runtime.NodeSystem
{
    public abstract class NodeGraph : ScriptableObject, ISerializeCallbacks
    {
        [SerializeField] int _nextID = 0;
        public int NextID => _nextID++;

        /// <summary> All nodes in the graph. <para/>
        /// See: <see cref="AddNode{T}"/> </summary>
        public List<Node> nodes = new List<Node>();

        public abstract Type[] NodeTypes { get; }

        /// <summary> Add a node to the graph by type (convenience method - will call the System.Type version) </summary>
        public T AddNode<T>() where T : Node
        {
            return AddNode(typeof(T)) as T;
        }
        
        /// <summary> Add a node to the graph by type </summary>
        public virtual Node AddNode(Type type)
        {
            // if it has a DisallowMultipleNodesAttribute and there is already one in the graph return null
            var attrib = type.GetCustomAttribute<Node.DisallowMultipleNodesAttribute>(true);
            if (attrib != null)
            {
                if (nodes.Where(n => n.GetType() == type).Count() >= attrib.max)
                {
                    Debug.LogError($"Only {attrib.max} nodes of type {type} are allowed in the graph!");
                    return null;
                }
            }

            Node node = Activator.CreateInstance(type) as Node;
            node.graph = this;
            nodes.Add(node);
            node.OnEnable();
            return node;
        }
        
        public Node GetNode<T>() where T : Node
        {
            return nodes.Where(n => n.GetType() == typeof(T)).FirstOrDefault();
        }

        public virtual Node GetNode(int instanceID)
        {
            return nodes.Where(n => n.InstanceID == instanceID).FirstOrDefault();
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual Node CopyNode(Node original)
        {
            Tag nodeTag = TagSerializer.Serialize(original);
            Node node = TagSerializer.Deserialize<Node>(nodeTag);
            node.graph = this;
            node.ClearConnections();
            nodes.Add(node);
            return node;
        }
        
        /// <summary> Safely remove a node and all its connections </summary>
        /// <param name="node"> The node to remove </param>
        public virtual void RemoveNode(Node node)
        {
            node.ClearConnections();
            nodes.Remove(node);
        }
        
        /// <summary> Remove all nodes and connections from the graph </summary>
        public virtual void Clear()
        {
            nodes.Clear();
        }
        
        /// <summary> Create a new deep copy of this graph </summary>
        public virtual NodeGraph Copy()
        {
            Tag graphTag = TagSerializer.Serialize(this);
            NodeGraph graph = TagSerializer.Deserialize<NodeGraph>(graphTag);
            // Instantiate all nodes inside the graph
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null) continue;
                Tag nodeTag = TagSerializer.Serialize(nodes[i]);
                Node node = TagSerializer.Deserialize<Node>(nodeTag);
                node.graph = graph;
                graph.nodes[i] = node;
            }
        
            // Redirect all connections
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] == null) continue;
                foreach (NodePort port in graph.nodes[i].Ports)
                {
                    port.Redirect(nodes, graph.nodes);
                }
            }
        
            return graph;
        }

        protected virtual void OnDestroy()
        {
            // Remove all nodes prior to graph destruction
            Clear();
        }

        public void PreSerialize() { }

        public void PostSerialize() { }

        public void PreDeserialize() { }

        public void PostDeserialize()
        {
            // Clear null nodes
            nodes.RemoveAll(n => n == null);
            foreach (Node node in nodes)
            {
                node.graph = this;
                node.VerifyConnections();
                node.OnEnable();
            }
        }

        #region Attributes
        /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted. </summary>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class RequireNodeAttribute : Attribute
        {
            public Type type0;
            public Type type1;
            public Type type2;
        
            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type)
            {
                this.type0 = type;
                this.type1 = null;
                this.type2 = null;
            }
        
            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type, Type type2)
            {
                this.type0 = type;
                this.type1 = type2;
                this.type2 = null;
            }
        
            /// <summary> Automatically ensures the existance of a certain node type, and prevents it from being deleted </summary>
            public RequireNodeAttribute(Type type, Type type2, Type type3)
            {
                this.type0 = type;
                this.type1 = type2;
                this.type2 = type3;
            }
        
            public bool Requires(Type type)
            {
                if (type == null) return false;
                if (type == type0) return true;
                else if (type == type1) return true;
                else if (type == type2) return true;
                return false;
            }
        }
        #endregion
    }
}
