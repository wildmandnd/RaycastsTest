using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class PrefabsReference : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject Prefab;
    public static Entity cubePrefab;

    public void Convert(Entity entity, EntityManager dstManager,
        GameObjectConversionSystem conversionSystem)
    {
        cubePrefab = conversionSystem.GetPrimaryEntity(Prefab);
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(Prefab);
    }
}
