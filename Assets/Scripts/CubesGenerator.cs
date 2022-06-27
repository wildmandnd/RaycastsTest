using System.Collections;
using System.Collections.Generic;
//using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Burst;

[AlwaysUpdateSystem]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class RaycastSystem : SystemBase
{
    NativeArray<RaycastHit> results;
    NativeArray<RaycastInput> commands;
    NativeArray<Entity> cubes;

    const int numOfEntities = 145;

    protected override void OnStartRunning()
    {
        //base.OnCreate();
        results = new NativeArray<RaycastHit>(numOfEntities * (numOfEntities-1), Allocator.Persistent);
        commands = new NativeArray<RaycastInput>(numOfEntities * (numOfEntities - 1), Allocator.Persistent);

        var randomArray = World.GetOrCreateSystem<RandomSystem>().RandomArray;

        var fixedStepSimulationSystemGroup = Unity.Entities.World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FixedStepSimulationSystemGroup>();
        fixedStepSimulationSystemGroup.FixedRateManager = null;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        cubes = em.Instantiate(PrefabsReference.cubePrefab, 10 * numOfEntities * numOfEntities, Allocator.Persistent);

        Entities
            .WithName("RandomizeTransforms")
            .WithAll<RayCasterTag>()
            .ForEach(
                (
                    int nativeThreadIndex, 
                    ref Translation trs
                ) =>
                {
                    var random = randomArray[nativeThreadIndex];

                    trs.Value = random.NextFloat3(-1000, 1000);

                    randomArray[nativeThreadIndex] = random;
                }
            )
            .WithNativeDisableParallelForRestriction(randomArray)
            .ScheduleParallel(Dependency).Complete();

        int ci = 0;

        for(int i = 0; i < numOfEntities; i++)
        {
            for(int j = 0; j < numOfEntities; j++)
            {
                // No self casts
                if (j == i) continue;

                commands[ci++] = new RaycastInput()
                {
                    Start = em.GetComponentData<Translation>(cubes[i]).Value,
                    End = em.GetComponentData<Translation>(cubes[j]).Value,
                    Filter = new CollisionFilter()
                    {
                        BelongsTo = ~0u,
                        CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                        GroupIndex = 0
                    }
                };
            }
        }

        UnityEngine.Debug.LogFormat("Total raycasts per frame: {0}, colliders: {1}", ci, cubes.Length);
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();

        results.Dispose();
        commands.Dispose();
        cubes.Dispose();
    }

    protected override void OnUpdate()
    {
        var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
        var collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;

        var handle = ScheduleBatchRayCast(collisionWorld, commands, results);
        handle.Complete();
    }

    [BurstCompile]
    public struct RaycastJob : IJobParallelFor
    {
        [ReadOnly] public CollisionWorld world;
        [ReadOnly] public NativeArray<RaycastInput> inputs;
        public NativeArray<RaycastHit> results;

        public void Execute(int index)
        {
            RaycastHit hit;
            world.CastRay(inputs[index], out hit);
            results[index] = hit;
        }
    }
    public static JobHandle ScheduleBatchRayCast(CollisionWorld world,
        NativeArray<RaycastInput> inputs, NativeArray<RaycastHit> results)
    {
        JobHandle rcj = new RaycastJob
        {
            inputs = inputs,
            results = results,
            world = world

        }.Schedule(inputs.Length, 4);
        return rcj;
    }
}

