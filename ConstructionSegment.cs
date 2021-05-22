using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DI_ConstructionSystem
{
    [ExecuteInEditMode]
    public class ConstructionSegment : MonoBehaviour
    {
        public ConstructionSnap[] snaps;
        [HideInInspector]
        public bool zeroCheck = false;

        private void Reset()
        {
            GetSnaps();
        }

        private void OnEnable()
        {
            GetSnaps();
            DetachAllSnaps();
            zeroCheck = true;
        }

        private void OnDisable()
        {
            DetachAllSnaps();
        }

        void GetSnaps()
        {
            snaps = GetComponentsInChildren<ConstructionSnap>();
            foreach (var snap in snaps)
            {
                snap.segment = this;
            }
        }

        void DetachAllSnaps()
        {
            if (snaps != null && snaps.Length > 0)
            {
                foreach (var snap in snaps)
                {
                    try
                    {
                        snap.ConnectedTo.ConnectedTo = null;
                    }
                    catch { }

                    snap.ConnectedTo = null;
                }
            }
        }
    }
}
