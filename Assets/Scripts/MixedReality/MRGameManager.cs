using UnityEngine;

namespace MRGame
{
    /// <summary>
    /// Raíz lógica del juego de Realidad Mixta. Contiene la mesa anclada y el contenedor de objetos
    /// interactivos, y avisa cuando el tablero está listo para recibir los elementos del juego.
    /// </summary>
    public class MRGameManager : MonoBehaviour
    {
        [SerializeField] private MRTableAnchor table;
        [Tooltip("Contenedor (hijo de la mesa) donde aparecen los objetos interactivos.")]
        [SerializeField] private Transform itemsRoot;

        /// <summary>Contenedor de los objetos del juego; se mueve junto con la mesa anclada.</summary>
        public Transform ItemsRoot => itemsRoot;

        /// <summary>True cuando la mesa ya está colocada y anclada.</summary>
        public bool TableReady => table != null && table.IsPlaced;

        private void OnEnable()
        {
            if (table != null) table.Placed += OnTablePlaced;
        }

        private void OnDisable()
        {
            if (table != null) table.Placed -= OnTablePlaced;
        }

        private void OnTablePlaced(Pose pose, bool isRealTable)
        {
            Debug.Log($"[MRGame] Juego listo. Mesa real: {isRealTable}. Los objetos aparecen en '{(itemsRoot != null ? itemsRoot.name : "?")}'.");
        }
    }
}
