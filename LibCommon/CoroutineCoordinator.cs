// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace LibCommon
{
    /// <summary>
    /// Allows coordinating between multiple coroutines so
    /// that if a coroutine overshoots its frametime budget,
    /// it can pause itself so that other coroutines can
    /// work in subsequent frames, then resume once the coast
    /// is clear.
    /// </summary>
    internal class CoroutineCoordinator
    {
        static Queue<(string, int)> yielders = [];
        static Queue<string> waiters = [];
        static Action<string> log;


        internal static void Init(Action<string> log)
        {
            CoroutineCoordinator.log = log;
            var go = GameObject.Find("CoroutineCoordinator");
            if (go == null)
            {
                go = new GameObject("CoroutineCoordinator");
                GameObject.DontDestroyOnLoad(go);
                var b = go.AddComponent<CoroutineCoordinatorBehaviour>();
                b.yielders = yielders;
                b.waiters = waiters;
                log?.Invoke("CoroutineCoordinator created");
            }
            else
            {
                waiters = [];
                yielders = [];

                foreach (var comp in go.GetComponents(typeof(MonoBehaviour)))
                {
                    if (comp.name.Contains("CoroutineCoordinator"))
                    {
                        var fref = AccessTools.FieldRefAccess<Queue<string>>(comp.GetType(), "waiters");
                        var gref = AccessTools.FieldRefAccess<Queue<(string, int)>>(comp.GetType(), "yielders");

                        waiters = fref(comp);
                        yielders = gref(comp);

                        if (waiters == null)
                        {
                            waiters = [];
                            yielders = [];
                            throw new InvalidOperationException("FieldCoordinator.waiters");
                        }
                        if (yielders == null)
                        {
                            waiters = [];
                            yielders = [];
                            throw new InvalidOperationException("FieldCoordinator.yielders");
                        }
                        log?.Invoke("CoroutineCoordinator Found");
                        return;
                    }
                }
                throw new InvalidOperationException("CoroutineCoordinator not found after all");
            }
        }

        internal static void Clear()
        {
            yielders.Clear();
            waiters.Clear();
        }

        internal static void Yield(string routineId, double elapsed)
        {
            int fc = Time.frameCount;
            log?.Invoke("CoroutineCoordinator-Yielding    [" + fc + "] " + routineId + " (" + elapsed + " ms)");
            yielders.Enqueue((routineId, fc));
        }

        internal static void Remove(string routineId)
        {
            // if it was waiting, remove it from the waiters

            var copy = new List<string>();
            while (waiters.TryDequeue(out var w))
            {
                if (w != routineId)
                {
                    copy.Add(w);
                }
            }

            foreach (var w in copy)
            {
                waiters.Enqueue(w);
            }

            // if it was yielding, remove it from the yielders
            var copy2 = new List<(string, int)>();
            while (yielders.TryDequeue(out var y))
            {
                if (y.Item1 != routineId)
                {
                    copy2.Add(y);
                }
            }
            yielders.Clear();
            foreach (var y in copy2)
            {
                yielders.Enqueue(y);
            }
            int fc = Time.frameCount;
            log?.Invoke("CoroutineCoordinator-Remove      [" + fc + "] " + routineId + " removed");
        }

        internal static bool CanRun(string routineId)
        {
            int fc = Time.frameCount;

            bool yieldedThisFrame = yielders.Any(e => e.Item2 == fc);
            bool routineInYield = yielders.Any(e => e.Item1 == routineId);

            // if someone yielded this frame
            if (yieldedThisFrame && !routineInYield)
            {
                log?.Invoke("CoroutineCoordinator-NoTimeLeft  [" + fc + "] " + routineId);
                // enqueue us as a waiter
                if (!waiters.Contains(routineId))
                {
                    waiters.Enqueue(routineId);
                }
                return false;
            }
            // is someone waiting
            if (waiters.TryPeek(out var w))
            {
                // its us, stop waiting and indicate runs
                if (w == routineId)
                {
                    log?.Invoke("CoroutineCoordinator-RunWaiter   [" + fc + "] " + routineId);
                    waiters.TryDequeue(out _);
                    return true;
                }

                // check if it is a yielder itself
                if (!routineInYield)
                {
                    // nope another waiter
                    log?.Invoke("CoroutineCoordinator-SkipWaiter  [" + fc + "] " + routineId + ", next waiter " + w);
                    // not us, enqueue as waiter
                    if (!waiters.Contains(routineId))
                    {
                        waiters.Enqueue(routineId);
                    }
                } 
                else
                {
                    log?.Invoke("CoroutineCoordinator-SkipYielder [" + fc + "] " + routineId + ", next waiter " + w);
                }
                return false;
            }
            // no one is waiting
            // check the yielders
            if (yielders.TryPeek(out var y))
            {
                // did we yield?
                // yes, resume us
                if (y.Item1 == routineId)
                {
                    log?.Invoke("CoroutineCoordinator-RunYielder  [" + fc + "] " + routineId + " after " + (fc - y.Item2) + " skips");
                    yielders.TryDequeue(out _);
                    return true;
                }
                // nope, wait our turn
                log?.Invoke("CoroutineCoordinator-SkipYielder [" + fc + "] " + routineId + ", next yielder " + y.Item1);
                return false;
            }
            // no yielders, no waiters, we can run
            log?.Invoke("CoroutineCoordinator-Free        [" + fc + "] " + routineId);

            return true;
        }

        internal class CoroutineCoordinatorBehaviour : MonoBehaviour
        {
            internal Queue<(string, int)> yielders;

            internal Queue<string> waiters;
        }
    }
}
