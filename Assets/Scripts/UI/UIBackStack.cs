using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.UI
{
    public interface IBackClosable
    {
        bool CloseByBack();
    }

    public static class UIBackStack
    {
        private static readonly List<IBackClosable> stack = new();

        public static event Action BackPressedWithoutTarget;

        public static void Push(IBackClosable closable)
        {
            if (closable == null)
            {
                return;
            }

            Remove(closable);
            stack.Add(closable);
        }

        public static void Remove(IBackClosable closable)
        {
            if (closable == null)
            {
                return;
            }

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(stack[i], closable))
                {
                    stack.RemoveAt(i);
                }
            }
        }

        public static bool TryCloseTop()
        {
            PruneDestroyedEntries();

            while (stack.Count > 0)
            {
                int index = stack.Count - 1;
                IBackClosable top = stack[index];
                if (top == null)
                {
                    stack.RemoveAt(index);
                    continue;
                }

                if (top.CloseByBack())
                {
                    return true;
                }

                stack.RemoveAt(index);
            }

            return false;
        }

        public static void HandleBackPressed()
        {
            if (TryCloseTop())
            {
                return;
            }

            BackPressedWithoutTarget?.Invoke();
        }

        private static void PruneDestroyedEntries()
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i] == null || stack[i] is UnityEngine.Object unityObject && unityObject == null)
                {
                    stack.RemoveAt(i);
                }
            }
        }
    }
}
