using UnityEngine;

namespace ValeriusCustomPrefabs
{
    // Terrain owns its physical collision mesh. SculptTile requests updates.
    public sealed class SculptColliderLink : MonoBehaviour
    {
        public SculptTile owner;
    }

    public sealed class TerrainCollision : MonoBehaviour
    {
        private TerrainPiece visual;
        private MeshCollider dynamicCollider;
        private Transform colliderFrame;

        private void Awake()
        {
            visual = GetComponent<TerrainPiece>();
        }

        private void Update()
        {
            if (dynamicCollider != null || visual == null || !visual.SculptReady) return;
            ZNetView view = GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || view.GetZDO() == null) return;
            Initialize();
        }

        private void Initialize()
        {
            if (dynamicCollider != null || visual == null) return;

            var go = new GameObject("VH_DeformableCollider");
            int pieceLayer = LayerMask.NameToLayer("piece");
            if (pieceLayer >= 0) go.layer = pieceLayer;
            go.transform.SetParent(transform, false);
            colliderFrame = go.transform;
            SyncColliderFrame();
            dynamicCollider = go.AddComponent<MeshCollider>();
            go.AddComponent<SculptColliderLink>().owner = GetComponent<SculptTile>();

            // Preserve existing replacement behaviour on live pieces.
            foreach (Collider old in GetComponentsInChildren<Collider>(true))
                if (old != dynamicCollider) old.enabled = false;
            RefreshMesh(visual.SculptMesh);
        }

        private void LateUpdate()
        {
            if (colliderFrame != null) SyncColliderFrame();
        }

        private void SyncColliderFrame()
        {
            if (colliderFrame == null || visual == null || visual.SculptTransform == null) return;
            Transform source = visual.SculptTransform;
            colliderFrame.position = source.position;
            colliderFrame.rotation = source.rotation;
            Vector3 parentScale = transform.lossyScale;
            Vector3 sourceScale = source.lossyScale;
            colliderFrame.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.0001f ? sourceScale.x / parentScale.x : 1f,
                Mathf.Abs(parentScale.y) > 0.0001f ? sourceScale.y / parentScale.y : 1f,
                Mathf.Abs(parentScale.z) > 0.0001f ? sourceScale.z / parentScale.z : 1f);
        }

        public void RefreshMesh(Mesh mesh)
        {
            if (dynamicCollider == null || mesh == null) return;
            dynamicCollider.sharedMesh = null;
            dynamicCollider.sharedMesh = mesh;
        }
    }
}
