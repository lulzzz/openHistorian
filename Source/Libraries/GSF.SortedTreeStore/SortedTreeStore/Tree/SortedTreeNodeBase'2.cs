﻿//******************************************************************************************************
//  SortedTreeNodeBase`2.cs - Gbtc
//
//  Copyright © 2013, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  4/16/2013 - Steven E. Chisholm
//       Generated original version of source code. 
//     
//******************************************************************************************************

using System;
using System.Collections.Generic;
using GSF.IO;

//ToDo: Reviewed

namespace GSF.SortedTreeStore.Tree
{
    /// <summary>
    /// A tree node in the SortedTree
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public abstract partial class SortedTreeNodeBase<TKey, TValue>
        : Node<TKey>
        where TKey : class, ISortedTreeValue<TKey>, new()
        where TValue : class, ISortedTreeValue<TValue>, new()
    {
        #region [ Members ]

        protected readonly int KeyValueSize;
        private Node<TKey> m_tempNode1;
        private Node<TKey> m_tempNode2;
        private int m_minRecordNodeBytes;
        private Func<uint> m_getNextNewNodeIndex;
        protected SortedTreeTypeMethodsBase<TValue> ValueMethods;
        protected SparseIndex<TKey> SparseIndex;
        private bool m_initialized;

        #endregion

        #region [ Constructors ]

        protected SortedTreeNodeBase(byte level, byte version)
            : base(level, version)
        {
            m_initialized = false;
            ValueMethods = new TValue().CreateValueMethods();
            KeyValueSize = KeySize + ValueMethods.Size;
        }

        /// <summary>
        /// Initializes the required parameters for this tree to function. Must be called once.
        /// </summary>
        /// <param name="stream">the stream to use.</param>
        /// <param name="blockSize">the size of each block</param>
        /// <param name="getNextNewNodeIndex"></param>
        /// <param name="sparseIndex"></param>
        public void Initialize(BinaryStreamBase stream, int blockSize, Func<uint> getNextNewNodeIndex, SparseIndex<TKey> sparseIndex)
        {
            if (m_initialized)
                throw new Exception("Duplicate calls to initialize");
            m_initialized = true;

            InitializeNode(stream, blockSize);

            m_tempNode1 = new Node<TKey>(stream, blockSize, Level, Version);
            m_tempNode2 = new Node<TKey>(stream, blockSize, Level, Version);
            SparseIndex = sparseIndex;
            m_minRecordNodeBytes = BlockSize >> 2;
            m_getNextNewNodeIndex = getNextNewNodeIndex;

            InitializeType();
        }

        #endregion

        #region [ Methods ]

        #region [ Override Methods ]

        /// <summary>
        /// Determines which sibling node that this node can be combined with.
        /// </summary>
        /// <param name="key">the key of the child node that needs to be checked.</param>
        /// <param name="canCombineLeft">outputs true if combining with left child is supported.</param>
        /// <param name="canCombineRight">outputs true if combining with right child is supported.</param>
        public void CanCombineWithSiblings(TKey key, out bool canCombineLeft, out bool canCombineRight)
        {
            if (Level == 0)
                throw new NotSupportedException("This function cannot be used at the leaf level.");

            NavigateToNode(key);

            int search = GetIndexOf(key);
            if (search < 0)
            {
                throw new KeyNotFoundException();
            }
            canCombineLeft = search > 0;
            canCombineRight = search < RecordCount - 1;
        }

        /// <summary>
        /// Navigates to the node that contains this key.
        /// </summary>
        /// <param name="key">They key of concern</param>
        private void NavigateToNode(TKey key)
        {
            if (!IsKeyInsideBounds(key))
                SetNodeIndex(SparseIndex.Get(key, Level));
        }

        /// <summary>
        /// Returns a tree scanner class.
        /// </summary>
        /// <returns></returns>
        public abstract SortedTreeScannerBase<TKey, TValue> CreateTreeScanner();

        #endregion

        #endregion
    }
}