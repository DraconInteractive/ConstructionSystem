using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DI_ConstructionSystem
{
    [ExecuteInEditMode]
    public class ConstructionSnap : MonoBehaviour
    {
        private ConstructionSnap _connectedTo;

        public ConstructionSnap ConnectedTo
        {
            get
            {
                return _connectedTo;
            }
            set
            {
                _connectedTo = value;
                connected = (_connectedTo != null);
            }
        }
        public bool connected;
        public ConstructionSegment segment;


    }
}
