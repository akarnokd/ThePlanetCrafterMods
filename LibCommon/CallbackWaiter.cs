// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

namespace LibCommon
{
    /// <summary>
    /// Class that holds a done and a success flag, set via Done()
    /// and can be used for wait in a coroutine for IsDone to turn true.
    /// Usage:
    /// <code>
    /// var cw = new CallbackWaiter();
    /// someApi(onComplete: cw.Done);
    /// while (!cw.IsDone) {
    ///    yield return null;
    /// }
    /// </code>
    /// </summary>
    public class CallbackWaiter
    {
        public bool IsDone { get; private set; }
        public bool IsSuccess { get; private set; }

        public void Done()
        {
            IsDone = true;
        }
        public void Done(bool success)
        {
            IsSuccess = success;
            IsDone = true;
        }

        public void Reset()
        {
            IsDone = false;
            IsSuccess = false;
        }
    }
}
