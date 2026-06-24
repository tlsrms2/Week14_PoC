using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public abstract class BossAction
    {
        public abstract IEnumerator Execute(BossActionContext context);
    }
}
