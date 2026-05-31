using System;
using System.Collections.Generic;
using System.Text;
using UTN_TestGame;

namespace UTN_Parcial2 {
    sealed class Chest : InteractableObject {
        private readonly int CollectibleCount = 3;
        public override int OnInteract() {
            Enabled = false;
            return CollectibleCount;
        }
    }
}
