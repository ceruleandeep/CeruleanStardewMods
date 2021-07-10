using System;
using System.Collections.Generic;

namespace PersonalEffects
{
    public interface IJsonAssetsApi
    {
        int GetObjectId(string name);
        IDictionary<string, int> GetAllObjectIds();
    }
}
