﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
namespace LuaInterface
{
    public class LuaScriptMgr
    {
        public static LuaScriptMgr Instance { get; private set; }

        public LuaState lua;
        public HashSet<string> fileList = null;
        Dictionary<string, LuaBase> dict = null;

        int unpackVec3 = 0;
        int unpackVec2 = 0;
        int unpackVec4 = 0;
        int unpackQuat = 0;
        int unpackColor = 0;
        int unpackRay = 0;
        int unpackBounds = 0;

        int packVec3 = 0;
        int packVec2 = 0;
        int packVec4 = 0;
        int packQuat = 0;
        LuaFunction packTouch = null;
        int packRay = 0;
        LuaFunction packRaycastHit = null;
        int packColor = 0;
        int packBounds = 0;

        int enumMetaRef = 0;
        int typeMetaRef = 0;
        int delegateMetaRef = 0;
        int iterMetaRef = 0;
        int arrayMetaRef = 0;
        
        static ObjectTranslator _translator = null;

        static HashSet<Type> checkBaseType = new HashSet<Type>();

        static LuaFunction traceback = null;


        public LuaScriptMgr()
        {
            if (!Util.CheckEnvironment()) return;

            Instance = this;
            LuaStatic.Load = Loader;
            lua = new LuaState();
            _translator = lua.GetTranslator();
            fileList = new HashSet<string>();
            dict = new Dictionary<string, LuaBase>();
            Bind();
        }


        void Bind()
        {
            IntPtr L = lua.L;
            BindArray(L);
            DelegateFactory.Register(L);
            LuaBinder.Bind(L);
        }

        void BindArray(IntPtr L)
        {
            LuaAPI.luaL_newmetatable(L, "luaNet_array");
            LuaAPI.lua_pushstring(L, "__index");
            LuaAPI.lua_pushstdcallcfunction(L, IndexArray);
            LuaAPI.lua_rawset(L, -3);
            LuaAPI.lua_pushstring(L, "__gc");
            LuaAPI.lua_pushstdcallcfunction(L, __gc);
            LuaAPI.lua_rawset(L, -3);
            LuaAPI.lua_pushstring(L, "__newindex");
            LuaAPI.lua_pushstdcallcfunction(L, NewIndexArray);
            LuaAPI.lua_rawset(L, -3);
            arrayMetaRef = LuaAPI.luaL_ref(lua.L, LuaAPI.LUA_REGISTRYINDEX);
            LuaAPI.lua_settop(L, 0);
        }

        public IntPtr GetL()
        {
            return lua.L;
        }

        public void LuaGC(params string[] param)
        {
            LuaAPI.lua_gc(lua.L, LuaGCOptions.LUA_GCCOLLECT, 0);
        }

        public void Start()
        {
            OnBundleLoaded();
            enumMetaRef = GetTypeMetaRef(typeof(System.Enum));
            typeMetaRef = GetTypeMetaRef(typeof(System.Type));
            delegateMetaRef = GetTypeMetaRef(typeof(System.Delegate));
            iterMetaRef = GetTypeMetaRef(typeof(IEnumerator));

            foreach (Type t in checkBaseType)
            {
                Debugger.LogWarning("BaseType {0} not register to lua", t.FullName);
            }
            checkBaseType.Clear();
        }

        int GetLuaReference(string str)
        {
            LuaFunction func = GetLuaFunction(str);
            if (func != null) return func.GetReference();
            return -1;
        }

        int GetTypeMetaRef(Type t)
        {
            string metaName = t.AssemblyQualifiedName;
            LuaAPI.luaL_getmetatable(lua.L, metaName);
            return LuaAPI.luaL_ref(lua.L, LuaAPI.LUA_REGISTRYINDEX);
        }

        void OnBundleLoaded()
        {
            DoFile("System/Global.lua");
            unpackVec3 = GetLuaReference("Vector3.Get");
            unpackVec2 = GetLuaReference("Vector2.Get");
            unpackVec4 = GetLuaReference("Vector4.Get");
            unpackQuat = GetLuaReference("Quaternion.Get");
            unpackColor = GetLuaReference("Color.Get");
            unpackRay = GetLuaReference("Ray.Get");
            unpackBounds = GetLuaReference("Bounds.Get");
            packVec3 = GetLuaReference("Vector3.New");
            packVec2 = GetLuaReference("Vector2.New");
            packVec4 = GetLuaReference("Vector4.New");
            packQuat = GetLuaReference("Quaternion.New");
            packRaycastHit = GetLuaFunction("Raycast.New");
            packColor = GetLuaReference("Color.New");
            packRay = GetLuaReference("Ray.New");
            packTouch = GetLuaFunction("Touch.New");
            packBounds = GetLuaReference("Bounds.New");
            traceback = GetLuaFunction("traceback");

            DoFile("System.Main");
            CallLuaFunction("Main");
        }

        void SafeRelease(ref LuaFunction func)
        {
            if (func != null)
            {
                func.Release();
                func = null;
            }
        }

        void SafeUnRef(ref int reference)
        {
            if (reference > 0)
            {
                LuaAPI.lua_unref(lua.L, reference);
                reference = -1;
            }
        }

        public void Destroy()
        {
            Instance = null;
            SafeUnRef(ref enumMetaRef);
            SafeUnRef(ref typeMetaRef);
            SafeUnRef(ref delegateMetaRef);
            SafeUnRef(ref iterMetaRef);
            SafeUnRef(ref arrayMetaRef);

            SafeRelease(ref packRaycastHit);
            SafeRelease(ref packTouch);

            LuaAPI.lua_gc(lua.L, LuaGCOptions.LUA_GCCOLLECT, 0);
            foreach (KeyValuePair<string, LuaBase> kv in dict)
            {
                kv.Value.Dispose();
            }
            dict.Clear();
            fileList.Clear();
            lua.Close();
            lua.Dispose();
            lua = null;

            DelegateFactory.Clear();
            LuaBinder.wrapList.Clear();
            Debugger.Log("Lua module destroy");
        }

        public object[] DoString(string str)
        {
            return lua.DoString(str);
        }

        public object[] DoFile(string fileName)
        {
            if (!fileList.Contains(fileName))
            {
                return lua.DoFile(fileName, null);
            }
            return null;
        }


        //LuaFunction not cached
        public object[] CallLuaFunction(string name, params object[] args)
        {
            LuaBase lb = null;
            if (dict.TryGetValue(name, out lb))
            {
                LuaFunction func = lb as LuaFunction;
                return func.Call(args);
            }
            else
            {
                IntPtr L = lua.L;
                LuaFunction func = null;
                int oldTop = LuaAPI.lua_gettop(L);

                if (PushLuaFunction(L, name))
                {
                    int reference = LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX);
                    func = new LuaFunction(reference, lua);
                    LuaAPI.lua_settop(L, oldTop);
                    object[] objs = func.Call(args);
                    func.Dispose();
                    return objs;
                }
                return null;
            }
        }

        //cache LuaFunction
        public LuaFunction GetLuaFunction(string name)
        {
            LuaBase func = null;
            if (!dict.TryGetValue(name, out func))
            {
                IntPtr L = lua.L;
                int oldTop = LuaAPI.lua_gettop(L);

                if (PushLuaFunction(L, name))
                {
                    int reference = LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX);
                    func = new LuaFunction(reference, lua);
                    func.name = name;
                    dict.Add(name, func);
                }
                else
                {
                    Debugger.LogError("Lua function {0} not exists", name);
                }
                LuaAPI.lua_settop(L, oldTop);
            }
            else
            {
                func.AddRef();
            }
            return func as LuaFunction;
        }

        public int GetFunctionRef(string name)
        {
            IntPtr L = lua.L;
            int oldTop = LuaAPI.lua_gettop(L);
            int reference = -1;

            if (PushLuaFunction(L, name))
            {
                reference = LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX);
            }
            else
            {
                Debugger.LogWarning("Lua function {0} not exists", name);
            }

            LuaAPI.lua_settop(L, oldTop);
            return reference;
        }

        public bool IsFuncExists(string name)
        {
            IntPtr L = lua.L;
            int oldTop = LuaAPI.lua_gettop(L);
            if (PushLuaFunction(L, name))
            {
                LuaAPI.lua_settop(L, oldTop);
                return true;
            }
            return false;
        }

        public byte[] Loader(string name)
        {
            byte[] str = null;
            fileList.Add(name);
            string luapath = Util.LuaResourcePath(name);
            TextAsset luaCode = Resources.Load<TextAsset>(luapath);
            if (luaCode != null) str = luaCode.bytes;
            return str;
        }

        static bool PushLuaTable(IntPtr L, string fullPath)
        {
            string[] path = fullPath.Split(new char[] { '.' });
            int oldTop = LuaAPI.lua_gettop(L);
            LuaAPI.lua_pushstring(L, path[0]);
            LuaAPI.lua_getglobal(L, path[0]);

            LuaTypes type = LuaAPI.lua_type(L, -1);
            if (type != LuaTypes.LUA_TTABLE)
            {
                LuaAPI.lua_settop(L, oldTop);
                LuaAPI.lua_pushnil(L);
                return false;
            }

            for (int i = 1; i < path.Length; i++)
            {
                LuaAPI.lua_pushstring(L, path[i]);
                LuaAPI.lua_rawget(L, -2);
                type = LuaAPI.lua_type(L, -1);
                if (type != LuaTypes.LUA_TTABLE)
                {
                    LuaAPI.lua_settop(L, oldTop);
                    Debugger.LogError("Push lua table {0} failed", fullPath);
                    return false;
                }
            }
            if (path.Length > 1)
            {
                LuaAPI.lua_insert(L, oldTop + 1);
                LuaAPI.lua_settop(L, oldTop + 1);
            }
            return true;
        }

        static bool PushLuaFunction(IntPtr L, string fullPath)
        {
            int oldTop = LuaAPI.lua_gettop(L);
            int pos = fullPath.LastIndexOf('.');

            if (pos > 0)
            {
                string tableName = fullPath.Substring(0, pos);
                if (PushLuaTable(L, tableName))
                {
                    string funcName = fullPath.Substring(pos + 1);
                    LuaAPI.lua_pushstring(L, funcName);
                    LuaAPI.lua_rawget(L, -2);
                }
                LuaTypes type = LuaAPI.lua_type(L, -1);
                if (type != LuaTypes.LUA_TFUNCTION)
                {
                    LuaAPI.lua_settop(L, oldTop);
                    return false;
                }
                LuaAPI.lua_insert(L, oldTop + 1);
                LuaAPI.lua_settop(L, oldTop + 1);
            }
            else
            {
                LuaAPI.lua_getglobal(L, fullPath);
                LuaTypes type = LuaAPI.lua_type(L, -1);
                if (type != LuaTypes.LUA_TFUNCTION)
                {
                    LuaAPI.lua_settop(L, oldTop);
                    return false;
                }
            }
            return true;
        }

        public void RemoveLuaRes(string name)
        {
            dict.Remove(name);
        }

        static void CreateTable(IntPtr L, string fullPath)
        {
            string[] path = fullPath.Split(new char[] { '.' });
            int oldTop = LuaAPI.lua_gettop(L);

            if (path.Length > 1)
            {
                LuaAPI.lua_getglobal(L, path[0]);
                LuaTypes type = LuaAPI.lua_type(L, -1);
                if (type == LuaTypes.LUA_TNIL)
                {
                    LuaAPI.lua_pop(L, 1);
                    LuaAPI.lua_createtable(L, 0, 0);
                    LuaAPI.lua_pushstring(L, path[0]);
                    LuaAPI.lua_pushvalue(L, -2);
                    LuaAPI.lua_settable(L, -3);
                    LuaAPI.lua_setglobal(L, path[0]);
                    LuaAPI.lua_getglobal(L, path[0]);
                }

                for (int i = 1; i < path.Length - 1; i++)
                {
                    LuaAPI.lua_pushstring(L, path[i]);
                    LuaAPI.lua_rawget(L, -2);
                    type = LuaAPI.lua_type(L, -1);
                    if (type == LuaTypes.LUA_TNIL)
                    {
                        LuaAPI.lua_pop(L, 1);
                        LuaAPI.lua_createtable(L, 0, 0);
                        LuaAPI.lua_pushstring(L, path[i]);
                        LuaAPI.lua_pushvalue(L, -2);
                        LuaAPI.lua_rawset(L, -4);
                    }
                }

                LuaAPI.lua_pushstring(L, path[path.Length - 1]);
                LuaAPI.lua_rawget(L, -2);
                type = LuaAPI.lua_type(L, -1);
                if (type == LuaTypes.LUA_TNIL)
                {
                    LuaAPI.lua_pop(L, 1);
                    LuaAPI.lua_createtable(L, 0, 0);
                    LuaAPI.lua_pushstring(L, path[path.Length - 1]);
                    LuaAPI.lua_pushvalue(L, -2);
                    LuaAPI.lua_rawset(L, -4);
                }
            }
            else
            {
                
                LuaAPI.lua_getglobal(L, path[0]);
                LuaTypes type = LuaAPI.lua_type(L, -1);
                if (type == LuaTypes.LUA_TNIL)
                {
                    LuaAPI.lua_pop(L, 1);
                    LuaAPI.lua_createtable(L, 0, 0);
                    LuaAPI.lua_pushstring(L, path[0]);
                    LuaAPI.lua_pushvalue(L, -2);
                    LuaAPI.lua_settable(L, -3);
                    LuaAPI.lua_setglobal(L, path[0]);
                    LuaAPI.lua_getglobal(L, path[0]);
                }
            }
            LuaAPI.lua_insert(L, oldTop + 1);
            LuaAPI.lua_settop(L, oldTop + 1);
        }

        //enum
        public static void RegisterLib(IntPtr L, string libName, Type t, LuaMethod[] regs)
        {
            CreateTable(L, libName);

            LuaAPI.luaL_getmetatable(L, t.AssemblyQualifiedName);

            if (LuaAPI.lua_isnil(L, -1))
            {
                LuaAPI.lua_pop(L, 1);
                LuaAPI.luaL_newmetatable(L, t.AssemblyQualifiedName);
            }

            LuaAPI.lua_pushstring(L, "ToLua_EnumIndex");
            LuaAPI.lua_rawget(L, LuaAPI.LUA_REGISTRYINDEX);
            LuaAPI.lua_setfield(L, -2, "__index");

            LuaAPI.lua_pushstring(L, "__gc");
            LuaAPI.lua_pushstdcallcfunction(L, __gc);
            LuaAPI.lua_rawset(L, -3);

            for (int i = 0; i < regs.Length - 1; i++)
            {
                LuaAPI.lua_pushstring(L, regs[i].name);
                LuaAPI.lua_pushstdcallcfunction(L, regs[i].func);
                LuaAPI.lua_rawset(L, -3);
            }

            int pos = regs.Length - 1;
            LuaAPI.lua_pushstring(L, regs[pos].name);
            LuaAPI.lua_pushstdcallcfunction(L, regs[pos].func);
            LuaAPI.lua_rawset(L, -4);

            LuaAPI.lua_setmetatable(L, -2);
            LuaAPI.lua_settop(L, 0);
        }

        public static void RegisterLib(IntPtr L, string libName, LuaMethod[] regs)
        {
            CreateTable(L, libName);
            for (int i = 0; i < regs.Length; i++)
            {
                LuaAPI.lua_pushstring(L, regs[i].name);
                LuaAPI.lua_pushstdcallcfunction(L, regs[i].func);
                LuaAPI.lua_rawset(L, -3);
            }
            LuaAPI.lua_settop(L, 0);
        }

        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        public static int __gc(IntPtr luaState)
        {
            int udata = LuaAPI.luanet_rawnetobj(luaState, 1);
            if (udata != -1)
            {
                ObjectTranslator translator = ObjectTranslator.FromState(luaState);
                translator.collectObject(udata);
            }
            return 0;
        }


        public static void RegisterLib(IntPtr L, string libName, Type t, LuaMethod[] regs, LuaField[] fields, Type baseType)
        {
            CreateTable(L, libName);
            LuaAPI.luaL_getmetatable(L, t.AssemblyQualifiedName);
            if (LuaAPI.lua_isnil(L, -1))
            {
                LuaAPI.lua_pop(L, 1);
                LuaAPI.luaL_newmetatable(L, t.AssemblyQualifiedName);
            }
            if (baseType != null)
            {
                LuaAPI.luaL_getmetatable(L, baseType.AssemblyQualifiedName);
                if (LuaAPI.lua_isnil(L, -1))
                {
                    LuaAPI.lua_pop(L, 1);
                    LuaAPI.luaL_newmetatable(L, baseType.AssemblyQualifiedName);
                    checkBaseType.Add(baseType);
                }
                else
                {
                    checkBaseType.Remove(baseType);
                }
                LuaAPI.lua_setmetatable(L, -2);
            }

            LuaAPI.tolua_setindex(L);
            LuaAPI.tolua_setnewindex(L);
            LuaAPI.lua_pushstring(L, "__call");
            LuaAPI.lua_pushstring(L, "ToLua_TableCall");
            LuaAPI.lua_rawget(L, LuaAPI.LUA_REGISTRYINDEX);
            LuaAPI.lua_rawset(L, -3);
            LuaAPI.lua_pushstring(L, "__gc");
            LuaAPI.lua_pushstdcallcfunction(L, __gc);
            LuaAPI.lua_rawset(L, -3);

            for (int i = 0; i < regs.Length; i++)
            {
                LuaAPI.lua_pushstring(L, regs[i].name);
                LuaAPI.lua_pushstdcallcfunction(L, regs[i].func);
                LuaAPI.lua_rawset(L, -3);
            }

            for (int i = 0; i < fields.Length; i++)
            {
                LuaAPI.lua_pushstring(L, fields[i].name);
                LuaAPI.lua_createtable(L, 2, 0);

                if (fields[i].getter != null)
                {
                    LuaAPI.lua_pushstdcallcfunction(L, fields[i].getter);
                    LuaAPI.xlua_rawseti(L, -2, 1);
                }
                if (fields[i].setter != null)
                {
                    LuaAPI.lua_pushstdcallcfunction(L, fields[i].setter);
                    LuaAPI.xlua_rawseti(L, -2, 2);
                }
                LuaAPI.lua_rawset(L, -3);
            }

            LuaAPI.lua_setmetatable(L, -2);
            LuaAPI.lua_settop(L, 0);
            checkBaseType.Remove(t);
        }

        public static double GetNumber(IntPtr L, int stackPos)
        {
            if (LuaAPI.lua_isnumber(L, stackPos))
            {
                return LuaAPI.lua_tonumber(L, stackPos);
            }
            LuaAPI.luaL_typerror(L, stackPos, "number");
            return 0;
        }

        public static bool GetBoolean(IntPtr L, int stackPos)
        {
            if (LuaAPI.lua_isboolean(L, stackPos))
            {
                return LuaAPI.lua_toboolean(L, stackPos);
            }
            LuaAPI.luaL_typerror(L, stackPos, "boolean");
            return false;
        }

        public static string GetString(IntPtr L, int stackPos)
        {
            string str = GetLuaString(L, stackPos);
            if (str == null)
            {
                LuaAPI.luaL_typerror(L, stackPos, "string");
            }
            return str;
        }

        public static LuaFunction GetFunction(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);
            if (luatype != LuaTypes.LUA_TFUNCTION)
            {
                return null;
            }
            LuaAPI.lua_pushvalue(L, stackPos);
            return new LuaFunction(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
        }

        public static LuaFunction ToLuaFunction(IntPtr L, int stackPos)
        {
            LuaAPI.lua_pushvalue(L, stackPos);
            return new LuaFunction(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
        }

        public static LuaFunction GetLuaFunction(IntPtr L, int stackPos)
        {
            LuaFunction func = GetFunction(L, stackPos);
            if (func == null)
            {
                LuaAPI.luaL_typerror(L, stackPos, "function");
                return null;
            }
            return func;
        }

        static LuaTable ToLuaTable(IntPtr L, int stackPos)
        {
            LuaAPI.lua_pushvalue(L, stackPos);
            return new LuaTable(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
        }

        static LuaTable GetTable(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);
            if (luatype != LuaTypes.LUA_TTABLE)
            {
                return null;
            }
            LuaAPI.lua_pushvalue(L, stackPos);
            return new LuaTable(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
        }

        public static LuaTable GetLuaTable(IntPtr L, int stackPos)
        {
            LuaTable table = GetTable(L, stackPos);
            if (table == null)
            {
                LuaAPI.luaL_typerror(L, stackPos, "table");
                return null;
            }
            return table;
        }

        public static object GetLuaObject(IntPtr L, int stackPos)
        {
            return GetTranslator(L).getRawNetObject(L, stackPos);
        }

        public static object GetNetObjectSelf(IntPtr L, int stackPos, string type)
        {
            object obj = GetTranslator(L).getRawNetObject(L, stackPos);
            if (obj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type));
                return null;
            }
            return obj;
        }

        public static object GetUnityObjectSelf(IntPtr L, int stackPos, string type)
        {
            object obj = GetTranslator(L).getRawNetObject(L, stackPos);
            UnityEngine.Object uObj = (UnityEngine.Object)obj;
            if (uObj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type));
                return null;
            }
            return obj;
        }

        public static object GetTrackedObjectSelf(IntPtr L, int stackPos, string type)
        {
            object obj = GetTranslator(L).getRawNetObject(L, stackPos);
            UnityEngine.TrackedReference uObj = (UnityEngine.TrackedReference)obj;
            if (uObj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type));
                return null;
            }
            return obj;
        }

        public static T GetNetObject<T>(IntPtr L, int stackPos)
        {
            return (T)GetNetObject(L, stackPos, typeof(T));
        }

        public static object GetNetObject(IntPtr L, int stackPos, Type type)
        {
            if (LuaAPI.lua_isnil(L, stackPos))
            {
                return null;
            }
            object obj = GetLuaObject(L, stackPos);
            if (obj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type.Name));
                return null;
            }

            Type objType = obj.GetType();
            if (type == objType || type.IsAssignableFrom(objType))
            {
                return obj;
            }
            LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got {1}", type.Name, objType.Name));
            return null;
        }

        public static T GetUnityObject<T>(IntPtr L, int stackPos) where T : UnityEngine.Object
        {
            return (T)GetUnityObject(L, stackPos, typeof(T));
        }

        public static UnityEngine.Object GetUnityObject(IntPtr L, int stackPos, Type type)
        {
            if (LuaAPI.lua_isnil(L, stackPos)) return null;

            object obj = GetLuaObject(L, stackPos);
            if (obj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type.Name));
                return null;
            }

            UnityEngine.Object uObj = (UnityEngine.Object)obj; // as UnityEngine.Object;        
            if (uObj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type.Name));
                return null;
            }

            Type objType = uObj.GetType();
            if (type == objType || objType.IsSubclassOf(type))
            {
                return uObj;
            }
            LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got {1}", type.Name, objType.Name));
            return null;
        }

        public static T GetTrackedObject<T>(IntPtr L, int stackPos) where T : UnityEngine.TrackedReference
        {
            return (T)GetTrackedObject(L, stackPos, typeof(T));
        }

        public static UnityEngine.TrackedReference GetTrackedObject(IntPtr L, int stackPos, Type type)
        {
            if (LuaAPI.lua_isnil(L, stackPos)) return null;

            object obj = GetLuaObject(L, stackPos);
            if (obj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type.Name));
                return null;
            }

            UnityEngine.TrackedReference uObj = obj as UnityEngine.TrackedReference;

            if (uObj == null)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", type.Name));
                return null;
            }

            Type objType = obj.GetType();
            if (type == objType || objType.IsSubclassOf(type))
            {
                return uObj;
            }

            LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got {1}", type.Name, objType.Name));
            return null;
        }

        public static Type GetTypeObject(IntPtr L, int stackPos)
        {
            object obj = GetLuaObject(L, stackPos);
            if (obj == null || obj.GetType() != monoType)
            {
                LuaAPI.luaL_argerror(L, stackPos, string.Format("Type expected, got {0}", obj == null ? "nil" : obj.GetType().Name));
            }
            return (Type)obj;
        }

        public static bool IsClassOf(Type child, Type parent)
        {
            return child == parent || parent.IsAssignableFrom(child);
        }

        static ObjectTranslator GetTranslator(IntPtr L)
        {
            if (_translator == null)
            {
                return ObjectTranslator.FromState(L);
            }
            return _translator;
        }

        //压入一个object变量
        public static void PushVarObject(IntPtr L, object o)
        {
            if (o == null)
            {
                LuaAPI.lua_pushnil(L);
                return;
            }

            Type t = o.GetType();

            if (t.IsValueType)
            {
                if (t == typeof(bool))
                {
                    bool b = (bool)o;
                    LuaAPI.lua_pushboolean(L, b);
                }
                else if (t.IsEnum)
                {
                    Push(L, (System.Enum)o);
                }
                else if (t.IsPrimitive)
                {
                    double d = Convert.ToDouble(o);
                    LuaAPI.lua_pushnumber(L, d);
                }
                else if (t == typeof(Vector3))
                {
                    Push(L, (Vector3)o);
                }
                else if (t == typeof(Vector2))
                {
                    Push(L, (Vector2)o);
                }
                else if (t == typeof(Vector4))
                {
                    Push(L, (Vector4)o);
                }
                else if (t == typeof(Quaternion))
                {
                    Push(L, (Quaternion)o);
                }
                else if (t == typeof(Color))
                {
                    Push(L, (Color)o);
                }
                else if (t == typeof(RaycastHit))
                {
                    Push(L, (RaycastHit)o);
                }
                else if (t == typeof(Touch))
                {
                    Push(L, (Touch)o);
                }
                else if (t == typeof(Ray))
                {
                    Push(L, (Ray)o);
                }
                else
                {
                    PushValue(L, o);
                }
            }
            else
            {
                if (t.IsArray)
                {
                    PushArray(L, (System.Array)o);
                }
                else if (t == typeof(LuaCSFunction))
                {
                    GetTranslator(L).pushFunction(L, (LuaCSFunction)o);
                }
                else if (t.IsSubclassOf(typeof(Delegate)))
                {
                    Push(L, (Delegate)o);
                }
                else if (IsClassOf(t, typeof(IEnumerator)))
                {
                    Push(L, (IEnumerator)o);
                }
                else if (t == typeof(string))
                {
                    string str = (string)o;
                    LuaAPI.lua_pushstring(L, str);
                }
                else if (t == typeof(LuaStringBuffer))
                {
                    LuaStringBuffer lsb = (LuaStringBuffer)o;
                    LuaAPI.lua_pushlstring(L, lsb.buffer, lsb.buffer.Length);
                }
                else if (t.IsSubclassOf(typeof(UnityEngine.Object)))
                {
                    UnityEngine.Object obj = (UnityEngine.Object)o;

                    if (obj == null)
                    {
                        LuaAPI.lua_pushnil(L);
                    }
                    else
                    {
                        PushObject(L, o);
                    }
                }
                else if (t == typeof(LuaTable))
                {
                    ((LuaTable)o).push(L);
                }
                else if (t == typeof(LuaFunction))
                {
                    ((LuaFunction)o).push(L);
                }
                else if (t == monoType)
                {
                    Push(L, (Type)o);
                }
                else if (t.IsSubclassOf(typeof(TrackedReference)))
                {
                    UnityEngine.TrackedReference obj = (UnityEngine.TrackedReference)o;

                    if (obj == null)
                    {
                        LuaAPI.lua_pushnil(L);
                    }
                    else
                    {
                        PushObject(L, o);
                    }
                }
                else
                {
                    PushObject(L, o);
                }
            }
        }

        //压入一个从object派生的变量
        public static void PushObject(IntPtr L, object o)
        {
            GetTranslator(L).pushObject(L, o, "luaNet_metatable");
        }

        public static void Push(IntPtr L, UnityEngine.Object obj)
        {
            PushObject(L, obj == null ? null : obj);
        }

        public static void Push(IntPtr L, TrackedReference obj)
        {
            PushObject(L, obj == null ? null : obj);
        }

        static void PushMetaObject(IntPtr L, ObjectTranslator translator, object o, int metaRef)
        {
            if (o == null)
            {
                LuaAPI.lua_pushnil(L);
                return;
            }

            int weakTableRef = translator.weakTableRef;
            int index = -1;
            bool found = translator.objectsBackMap.TryGetValue(o, out index);
            if (found)
            {
                if (LuaAPI.tolua_pushudata(L, weakTableRef, index)) return;
                translator.collectObject(index);
            }
            index = translator.addObject(o, false);
            LuaAPI.tolua_pushnewudata(L, metaRef, weakTableRef, index);
        }

        public static void Push(IntPtr L, Type o)
        {
            LuaScriptMgr mgr = GetMgrFromLuaState(L);
            ObjectTranslator translator = mgr.lua.translator;
            PushMetaObject(L, translator, o, mgr.typeMetaRef);
        }

        public static void Push(IntPtr L, Delegate o)
        {
            LuaScriptMgr mgr = GetMgrFromLuaState(L);
            ObjectTranslator translator = mgr.lua.translator;
            PushMetaObject(L, translator, o, mgr.delegateMetaRef);
        }

        public static void Push(IntPtr L, IEnumerator o)
        {
            LuaScriptMgr mgr = GetMgrFromLuaState(L);
            ObjectTranslator translator = mgr.lua.translator;
            PushMetaObject(L, translator, o, mgr.iterMetaRef);
        }

        public static void PushArray(IntPtr L, System.Array o)
        {
            LuaScriptMgr mgr = GetMgrFromLuaState(L);
            ObjectTranslator translator = mgr.lua.translator;
            PushMetaObject(L, translator, o, mgr.arrayMetaRef);
        }

        public static void PushValue(IntPtr L, object obj)
        {
            GetTranslator(L).PushValueResult(L, obj);
        }

        public static void Push(IntPtr L, bool b)
        {
            LuaAPI.lua_pushboolean(L, b);
        }

        public static void Push(IntPtr L, string str)
        {
            LuaAPI.lua_pushstring(L, str);
        }

        public static void Push(IntPtr L, char d)
        {
            LuaAPI.lua_pushinteger(L, d);
        }

        public static void Push(IntPtr L, sbyte d)
        {
            LuaAPI.lua_pushinteger(L, d);
        }

        public static void Push(IntPtr L, byte d)
        {
            LuaAPI.lua_pushinteger(L, d);
        }

        public static void Push(IntPtr L, short d)
        {
            LuaAPI.lua_pushinteger(L, d);
        }

        public static void Push(IntPtr L, ushort d)
        {
            LuaAPI.lua_pushinteger(L, d);
        }

        public static void Push(IntPtr L, int d)
        {
            LuaAPI.lua_pushinteger(L, d);
        }

        public static void Push(IntPtr L, uint d)
        {
            LuaAPI.lua_pushnumber(L, d);
        }

        public static void Push(IntPtr L, long d)
        {
            LuaAPI.lua_pushnumber(L, d);
        }

        public static void Push(IntPtr L, ulong d)
        {
            LuaAPI.lua_pushnumber(L, d);
        }

        public static void Push(IntPtr L, float d)
        {
            LuaAPI.lua_pushnumber(L, d);
        }

        public static void Push(IntPtr L, decimal d)
        {
            LuaAPI.lua_pushnumber(L, (double)d);
        }

        public static void Push(IntPtr L, double d)
        {
            LuaAPI.lua_pushnumber(L, d);
        }

        public static void Push(IntPtr L, IntPtr p)
        {
            LuaAPI.lua_pushlightuserdata(L, p);
        }

        public static void Push(IntPtr L, ILuaGeneratedType o)
        {
            if (o == null)
            {
                LuaAPI.lua_pushnil(L);
            }
            else
            {
                LuaTable table = o.__luaInterface_getLuaTable();
                table.push(L);
            }
        }

        public static void Push(IntPtr L, LuaTable table)
        {
            if (table == null)
            {
                LuaAPI.lua_pushnil(L);
            }
            else
            {
                table.push(L);
            }
        }

        public static void Push(IntPtr L, LuaFunction func)
        {
            if (func == null)
            {
                LuaAPI.lua_pushnil(L);
            }
            else
            {
                func.push();
            }
        }

        public static void Push(IntPtr L, LuaCSFunction func)
        {
            if (func == null)
            {
                LuaAPI.lua_pushnil(L);
                return;
            }

            GetTranslator(L).pushFunction(L, func);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0)
        {
            return CheckType(L, type0, begin);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3, Type type4)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3) && CheckType(L, type4, begin + 4);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3, Type type4, Type type5)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3) && CheckType(L, type4, begin + 4) &&
                   CheckType(L, type5, begin + 5);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3, Type type4, Type type5, Type type6)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3) && CheckType(L, type4, begin + 4) &&
                   CheckType(L, type5, begin + 5) && CheckType(L, type6, begin + 6);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3, Type type4, Type type5, Type type6, Type type7)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3) && CheckType(L, type4, begin + 4) &&
                   CheckType(L, type5, begin + 5) && CheckType(L, type6, begin + 6) && CheckType(L, type7, begin + 7);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3, Type type4, Type type5, Type type6, Type type7, Type type8)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3) && CheckType(L, type4, begin + 4) &&
                   CheckType(L, type5, begin + 5) && CheckType(L, type6, begin + 6) && CheckType(L, type7, begin + 7) && CheckType(L, type8, begin + 8);
        }

        public static bool CheckTypes(IntPtr L, int begin, Type type0, Type type1, Type type2, Type type3, Type type4, Type type5, Type type6, Type type7, Type type8, Type type9)
        {
            return CheckType(L, type0, begin) && CheckType(L, type1, begin + 1) && CheckType(L, type2, begin + 2) && CheckType(L, type3, begin + 3) && CheckType(L, type4, begin + 4) &&
                   CheckType(L, type5, begin + 5) && CheckType(L, type6, begin + 6) && CheckType(L, type7, begin + 7) && CheckType(L, type8, begin + 8) && CheckType(L, type9, begin + 9);
        }

        //当进入这里时势必会有一定的gc alloc, 因为params Type[]会分配内存, 可以像上面扩展来避免gc alloc
        public static bool CheckTypes(IntPtr L, int begin, params Type[] types)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (!CheckType(L, types[i], i + begin))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool CheckParamsType(IntPtr L, Type t, int begin, int count)
        {
            //默认都可以转 object
            if (t == typeof(object))
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (!CheckType(L, t, i + begin))
                {
                    return false;
                }
            }
            return true;
        }

        static bool CheckTableType(IntPtr L, Type t, int stackPos)
        {
            if (t.IsArray)
            {
                return true;
            }
            else if (t == typeof(LuaTable))
            {
                return true;
            }
            else if (t.IsValueType)
            {
                int oldTop = LuaAPI.lua_gettop(L);
                LuaAPI.lua_pushvalue(L, stackPos);
                LuaAPI.lua_pushstring(L, "class");
                LuaAPI.lua_gettable(L, -2);

                string cls = LuaAPI.lua_tostring(L, -1);
                LuaAPI.lua_settop(L, oldTop);

                if (cls == "Vector3")
                {
                    return t == typeof(Vector3);
                }
                else if (cls == "Vector2")
                {
                    return t == typeof(Vector2);
                }
                else if (cls == "Quaternion")
                {
                    return t == typeof(Quaternion);
                }
                else if (cls == "Color")
                {
                    return t == typeof(Color);
                }
                else if (cls == "Vector4")
                {
                    return t == typeof(Vector4);
                }
                else if (cls == "Ray")
                {
                    return t == typeof(Ray);
                }
            }
            return false;
        }

        public static bool CheckType(IntPtr L, Type t, int pos)
        {
            //默认都可以转 object
            if (t == typeof(object))
            {
                return true;
            }

            LuaTypes luaType = LuaAPI.lua_type(L, pos);

            switch (luaType)
            {
                case LuaTypes.LUA_TNUMBER:
                    return t.IsPrimitive;
                case LuaTypes.LUA_TSTRING:
                    return t == typeof(string);
                case LuaTypes.LUA_TUSERDATA:
                    return CheckUserData(L, luaType, t, pos);
                case LuaTypes.LUA_TBOOLEAN:
                    return t == typeof(bool);
                case LuaTypes.LUA_TFUNCTION:
                    return t == typeof(LuaFunction);
                case LuaTypes.LUA_TTABLE:
                    return CheckTableType(L, t, pos);
                case LuaTypes.LUA_TNIL:
                    return t == null;
                default:
                    break;
            }

            return false;
        }

        static Type monoType = typeof(Type).GetType();

        static bool CheckUserData(IntPtr L, LuaTypes luaType, Type t, int pos)
        {
            object obj = GetLuaObject(L, pos);
            Type type = obj.GetType();

            if (t == type)
            {
                return true;
            }
            if (t == typeof(Type))
            {
                return type == monoType;
            }
            else
            {
                return t.IsAssignableFrom(type);
            }
        }

        public static object[] GetParamsObject(IntPtr L, int stackPos, int count)
        {
            List<object> list = new List<object>();
            object obj = null;

            while (count > 0)
            {
                obj = GetVarObject(L, stackPos);

                ++stackPos;
                --count;

                if (obj != null)
                {
                    list.Add(obj);
                }
                else
                {
                    LuaAPI.luaL_argerror(L, stackPos, "object expected, got nil");
                    break;
                }
            }
            return list.ToArray();
        }

        public static T[] GetParamsObject<T>(IntPtr L, int stackPos, int count)
        {
            List<T> list = new List<T>();
            T obj = default(T);

            while (count > 0)
            {
                object tmp = GetLuaObject(L, stackPos);

                ++stackPos;
                --count;

                if (tmp != null && tmp.GetType() == typeof(T))
                {
                    obj = (T)tmp;
                    list.Add(obj);
                }
                else
                {
                    LuaAPI.luaL_argerror(L, stackPos, string.Format("{0} expected, got nil", typeof(T).Name));
                    break;
                }
            }

            return list.ToArray();
        }

        public static T[] GetArrayObject<T>(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);
            if (luatype == LuaTypes.LUA_TTABLE)
            {
                int index = 1;
                T val = default(T);
                List<T> list = new List<T>();
                LuaAPI.lua_pushvalue(L, stackPos);
                Type t = typeof(T);

                do
                {
                    LuaAPI.xlua_rawgeti(L, -1, index);
                    luatype = LuaAPI.lua_type(L, -1);

                    if (luatype == LuaTypes.LUA_TNIL)
                    {
                        LuaAPI.lua_pop(L, 1);
                        return list.ToArray(); ;
                    }
                    else if (!CheckType(L, t, -1))
                    {
                        LuaAPI.lua_pop(L, 1);
                        break;
                    }

                    val = (T)GetVarObject(L, -1);
                    list.Add(val);
                    LuaAPI.lua_pop(L, 1);
                    ++index;
                } while (true);
            }
            else if (luatype == LuaTypes.LUA_TUSERDATA)
            {
                T[] ret = GetNetObject<T[]>(L, stackPos);

                if (ret != null)
                {
                    return (T[])ret;
                }
            }
            else if (luatype == LuaTypes.LUA_TNIL)
            {
                return null;
            }

            LuaAPI.luaL_error(L, string.Format("invalid arguments to method: {0}, pos {1}", GetErrorFunc(1), stackPos));
            return null;
        }

        static string GetErrorFunc(int skip)
        {
            StackFrame sf = null;
            string file = string.Empty;
            StackTrace st = new StackTrace(skip, true);
            int pos = 0;

            do
            {
                sf = st.GetFrame(pos++);
                file = sf.GetFileName();
                file = Path.GetFileName(file);
            } while (!file.Contains("Wrap."));

            int index1 = file.LastIndexOf('\\');
            int index2 = file.LastIndexOf("Wrap.");
            string className = file.Substring(index1 + 1, index2 - index1 - 1);
            return string.Format("{0}.{1}", className, sf.GetMethod().Name);
        }

        public static string GetLuaString(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);
            string retVal = null;

            if (luatype == LuaTypes.LUA_TSTRING)
            {
                retVal = LuaAPI.lua_tostring(L, stackPos);
            }
            else if (luatype == LuaTypes.LUA_TUSERDATA)
            {
                object obj = GetLuaObject(L, stackPos);

                if (obj == null)
                {
                    LuaAPI.luaL_argerror(L, stackPos, "string expected, got nil");
                    return string.Empty;
                }

                if (obj.GetType() == typeof(string))
                {
                    retVal = (string)obj;
                }
                else
                {
                    retVal = obj.ToString();
                }
            }
            else if (luatype == LuaTypes.LUA_TNUMBER)
            {
                double d = LuaAPI.lua_tonumber(L, stackPos);
                retVal = d.ToString();
            }
            else if (luatype == LuaTypes.LUA_TBOOLEAN)
            {
                bool b = LuaAPI.lua_toboolean(L, stackPos);
                retVal = b.ToString();
            }
            else if (luatype == LuaTypes.LUA_TNIL)
            {
                return retVal;
            }
            else
            {
                LuaAPI.lua_getglobal(L, "tostring");
                LuaAPI.lua_pushvalue(L, stackPos);
                LuaAPI.lua_pcall(L, 1, 1,0);
                retVal = LuaAPI.lua_tostring(L, -1);
                LuaAPI.lua_pop(L, 1);
            }
            return retVal;
        }

        public static string[] GetParamsString(IntPtr L, int stackPos, int count)
        {
            List<string> list = new List<string>();
            string obj = null;

            while (count > 0)
            {
                obj = GetLuaString(L, stackPos);
                ++stackPos;
                --count;
                if (obj == null)
                {
                    LuaAPI.luaL_argerror(L, stackPos, "string expected, got nil");
                    break;
                }
                list.Add(obj);
            }
            return list.ToArray();
        }

        public static string[] GetArrayString(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);

            if (luatype == LuaTypes.LUA_TTABLE)
            {
                int index = 1;
                string retVal = null;
                List<string> list = new List<string>();
                LuaAPI.lua_pushvalue(L, stackPos);

                while (true)
                {
                    LuaAPI.xlua_rawgeti(L, -1, index);
                    luatype = LuaAPI.lua_type(L, -1);

                    if (luatype == LuaTypes.LUA_TNIL)
                    {
                        LuaAPI.lua_pop(L, 1);
                        return list.ToArray();
                    }
                    else
                    {
                        retVal = GetLuaString(L, -1);
                    }
                    list.Add(retVal);
                    LuaAPI.lua_pop(L, 1);
                    ++index;
                }
            }
            else if (luatype == LuaTypes.LUA_TUSERDATA)
            {
                string[] ret = GetNetObject<string[]>(L, stackPos);

                if (ret != null)
                {
                    return (string[])ret;
                }
            }
            else if (luatype == LuaTypes.LUA_TNIL)
            {
                return null;
            }

            LuaAPI.luaL_error(L, string.Format("invalid arguments to method: {0}, pos {1}", GetErrorFunc(1), stackPos));
            return null;
        }

        public static T[] GetArrayNumber<T>(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);

            if (luatype == LuaTypes.LUA_TTABLE)
            {
                int index = 1;
                T ret = default(T);
                List<T> list = new List<T>();
                LuaAPI.lua_pushvalue(L, stackPos);

                while (true)
                {
                    LuaAPI.xlua_rawgeti(L, -1, index);
                    luatype = LuaAPI.lua_type(L, -1);

                    if (luatype == LuaTypes.LUA_TNIL)
                    {
                        LuaAPI.lua_pop(L, 1);
                        return list.ToArray();
                    }
                    else if (luatype != LuaTypes.LUA_TNUMBER)
                    {
                        break;
                    }

                    ret = (T)Convert.ChangeType(LuaAPI.lua_tonumber(L, -1), typeof(T));
                    list.Add(ret);
                    LuaAPI.lua_pop(L, 1);
                    ++index;
                }
            }
            else if (luatype == LuaTypes.LUA_TUSERDATA)
            {
                T[] ret = GetNetObject<T[]>(L, stackPos);
                if (ret != null)
                {
                    return (T[])ret;
                }
            }
            else if (luatype == LuaTypes.LUA_TNIL)
            {
                return null;
            }

            LuaAPI.luaL_error(L, string.Format("invalid arguments to method: {0}, pos {1}", GetErrorFunc(1), stackPos));
            return null;
        }

        public static bool[] GetArrayBool(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);

            if (luatype == LuaTypes.LUA_TTABLE)
            {
                int index = 1;
                List<bool> list = new List<bool>();
                LuaAPI.lua_pushvalue(L, stackPos);

                while (true)
                {
                    LuaAPI.xlua_rawgeti(L, -1, index);
                    luatype = LuaAPI.lua_type(L, -1);

                    if (luatype == LuaTypes.LUA_TNIL)
                    {
                        LuaAPI.lua_pop(L, 1);
                        return list.ToArray();
                    }
                    else if (luatype != LuaTypes.LUA_TNUMBER)
                    {
                        break;
                    }

                    bool ret = LuaAPI.lua_toboolean(L, -1);
                    list.Add(ret);
                    LuaAPI.lua_pop(L, 1);
                    ++index;
                }
            }
            else if (luatype == LuaTypes.LUA_TUSERDATA)
            {
                bool[] ret = GetNetObject<bool[]>(L, stackPos);

                if (ret != null)
                {
                    return (bool[])ret;
                }
            }
            else if (luatype == LuaTypes.LUA_TNIL)
            {
                return null;
            }

            LuaAPI.luaL_error(L, string.Format("invalid arguments to method: {0}, pos {1}", GetErrorFunc(1), stackPos));
            return null;
        }

        public static LuaStringBuffer GetStringBuffer(IntPtr L, int stackPos)
        {
            LuaTypes luatype = LuaAPI.lua_type(L, stackPos);

            if (luatype == LuaTypes.LUA_TNIL)
            {
                return null;
            }
            else if (luatype != LuaTypes.LUA_TSTRING)
            {
                LuaAPI.luaL_typerror(L, stackPos, "string");
                return null;
            }

            int len = 0;
            IntPtr buffer = LuaAPI.lua_tolstring(L, stackPos, out len);
            return new LuaStringBuffer(buffer, (int)len);
        }

        public static void SetValueObject(IntPtr L, int pos, object obj)
        {
            GetTranslator(L).SetValueObject(L, pos, obj);
        }

        public static void CheckArgsCount(IntPtr L, int count)
        {
            int c = LuaAPI.lua_gettop(L);

            if (c != count)
            {
                string str = string.Format("no overload for method '{0}' takes '{1}' arguments", GetErrorFunc(1), c);
                LuaAPI.luaL_error(L, str);
            }
        }

        public static object GetVarTable(IntPtr L, int stackPos)
        {
            object o = null;
            int oldTop = LuaAPI.lua_gettop(L);
            LuaAPI.lua_pushvalue(L, stackPos);
            LuaAPI.lua_pushstring(L, "class");
            LuaAPI.lua_gettable(L, -2);

            if (LuaAPI.lua_isnil(L, -1))
            {
                LuaAPI.lua_settop(L, oldTop);
                LuaAPI.lua_pushvalue(L, stackPos);
                o = new LuaTable(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
            }
            else
            {
                string cls = LuaAPI.lua_tostring(L, -1);
                LuaAPI.lua_settop(L, oldTop);

                stackPos = stackPos > 0 ? stackPos : stackPos + oldTop + 1;

                if (cls == "Vector3")
                {
                    o = GetVector3(L, stackPos);
                }
                else if (cls == "Vector2")
                {
                    o = GetVector2(L, stackPos);
                }
                else if (cls == "Quaternion")
                {
                    o = GetQuaternion(L, stackPos);
                }
                else if (cls == "Color")
                {
                    o = GetColor(L, stackPos);
                }
                else if (cls == "Vector4")
                {
                    o = GetVector4(L, stackPos);
                }
                else if (cls == "Ray")
                {
                    o = GetRay(L, stackPos);
                }
                else if (cls == "Bounds")
                {
                    o = GetBounds(L, stackPos);
                }
                else
                {
                    LuaAPI.lua_pushvalue(L, stackPos);
                    o = new LuaTable(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
                }
            }
            return o;
        }

        //读取object类型，object为万用类型, 能读取所有从lua传递的参数
        public static object GetVarObject(IntPtr L, int stackPos)
        {
            LuaTypes type = LuaAPI.lua_type(L, stackPos);

            switch (type)
            {
                case LuaTypes.LUA_TNUMBER:
                    return LuaAPI.lua_tonumber(L, stackPos);
                case LuaTypes.LUA_TSTRING:
                    return LuaAPI.lua_tostring(L, stackPos);
                case LuaTypes.LUA_TUSERDATA:
                    {
                        int udata = LuaAPI.luanet_rawnetobj(L, stackPos);
                        if (udata != -1)
                        {
                            object obj = null;
                            GetTranslator(L).objects.TryGetValue(udata, out obj);
                            return obj;
                        }
                        else
                        {
                            return null;
                        }
                    }
                case LuaTypes.LUA_TBOOLEAN:
                    return LuaAPI.lua_toboolean(L, stackPos);
                case LuaTypes.LUA_TTABLE:
                    return GetVarTable(L, stackPos);
                case LuaTypes.LUA_TFUNCTION:
                    LuaAPI.lua_pushvalue(L, stackPos);
                    return new LuaFunction(LuaAPI.luaL_ref(L, LuaAPI.LUA_REGISTRYINDEX), GetTranslator(L).interpreter);
                default:
                    return null;
            }
        }


        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        public static int IndexArray(IntPtr L)
        {
            Array obj = GetLuaObject(L, 1) as Array;
            if (obj == null)
            {
                LuaAPI.luaL_error(L, "trying to index an invalid Array reference");
                LuaAPI.lua_pushnil(L);
                return 1;
            }
            LuaTypes luaType = LuaAPI.lua_type(L, 2);
            if (luaType == LuaTypes.LUA_TNUMBER)
            {
                int index = (int)LuaAPI.lua_tonumber(L, 2);

                if (index >= obj.Length)
                {
                    LuaAPI.luaL_error(L, "array index out of bounds: " + index + " " + obj.Length);
                    return 0;
                }
                object val = obj.GetValue(index);
                if (val == null)
                {
                    LuaAPI.luaL_error(L, string.Format("array index {0} is null", index));
                    return 0;
                }
                PushVarObject(L, val);
            }
            else if (luaType == LuaTypes.LUA_TSTRING)
            {
                string field = GetLuaString(L, 2);

                if (field == "Length")
                {
                    Push(L, obj.Length);
                }
            }
            return 1;
        }


        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        public static int NewIndexArray(IntPtr L)
        {
            Array obj = GetLuaObject(L, 1) as Array;
            if (obj == null)
            {
                LuaAPI.luaL_error(L, "trying to index and invalid object reference");
                return 0;
            }

            int index = (int)GetNumber(L, 2);
            object val = GetVarObject(L, 3);
            Type type = obj.GetType().GetElementType();
            if (!CheckType(L, type, 3))
            {
                LuaAPI.luaL_error(L, "trying to set object type is not correct");
                return 0;
            }
            val = Convert.ChangeType(val, type);
            obj.SetValue(val, index);
            return 0;
        }


        public static void DumpStack(IntPtr L)
        {
            int top = LuaAPI.lua_gettop(L);
            for (int i = 1; i <= top; i++)
            {
                LuaTypes t = LuaAPI.lua_type(L, i);

                switch (t)
                {
                    case LuaTypes.LUA_TSTRING:
                        Debugger.Log(LuaAPI.lua_tostring(L, i));
                        break;
                    case LuaTypes.LUA_TBOOLEAN:
                        Debugger.Log(LuaAPI.lua_toboolean(L, i).ToString());
                        break;
                    case LuaTypes.LUA_TNUMBER:
                        Debugger.Log(LuaAPI.lua_tonumber(L, i).ToString());
                        break;
                    default:
                        Debugger.Log(t.ToString());
                        break;
                }
            }
        }

        static Dictionary<Enum, object> enumMap = new Dictionary<Enum, object>();

        static object GetEnumObj(Enum e)
        {
            object o = null;
            if (!enumMap.TryGetValue(e, out o))
            {
                o = e;
                enumMap.Add(e, o);
            }
            return o;
        }

        //枚举是值类型
        public static void Push(IntPtr L, System.Enum e)
        {
            LuaScriptMgr mgr = GetMgrFromLuaState(L);
            ObjectTranslator translator = mgr.lua.translator;
            int weakTableRef = translator.weakTableRef;
            object obj = GetEnumObj(e);
            int index = -1;
            bool found = translator.objectsBackMap.TryGetValue(obj, out index);

            if (found)
            {
                if (LuaAPI.tolua_pushudata(L, weakTableRef, index))
                {
                    return;
                }
                translator.collectObject(index);
            }
            index = translator.addObject(obj, false);
            LuaAPI.tolua_pushnewudata(L, mgr.enumMetaRef, weakTableRef, index);
        }

        public static void Push(IntPtr L, LuaStringBuffer lsb)
        {
            if (lsb != null && lsb.buffer != null)
            {
                LuaAPI.lua_pushlstring(L, lsb.buffer, lsb.buffer.Length);
            }
            else
            {
                LuaAPI.lua_pushnil(L);
            }
        }

        public static LuaScriptMgr GetMgrFromLuaState(IntPtr L)
        {
            return Instance;
        }

        public static void ThrowLuaException(IntPtr L)
        {
            string err = LuaAPI.lua_tostring(L, -1);
            if (err == null) err = "Unknown Lua Error";
            throw new LuaScriptException(err.ToString(), "");
        }

        //无缝兼容原生写法 transform.position = v3          
        public static Vector3 GetVector3(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float x = 0, y = 0, z = 0;
            LuaAPI.tolua_getfloat3(L, luaMgr.unpackVec3, stackPos, ref x, ref y, ref z);
            return new Vector3(x, y, z);
        }

        public static void Push(IntPtr L, Vector3 v3)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.tolua_pushfloat3(L, luaMgr.packVec3, v3.x, v3.y, v3.z);
        }

        public static void Push(IntPtr L, Quaternion q)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.tolua_pushfloat4(L, luaMgr.packQuat, q.x, q.y, q.z, q.w);
        }

        public static Quaternion GetQuaternion(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float x = 0, y = 0, z = 0, w = 1;
            LuaAPI.tolua_getfloat4(L, luaMgr.unpackQuat, stackPos, ref x, ref y, ref z, ref w);
            return new Quaternion(x, y, z, w);
        }

        public static Vector2 GetVector2(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float x = 0, y = 0;
            LuaAPI.tolua_getfloat2(L, luaMgr.unpackVec2, stackPos, ref x, ref y);
            return new Vector2(x, y);
        }

        public static void Push(IntPtr L, Vector2 v2)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.tolua_pushfloat2(L, luaMgr.packVec2, v2.x, v2.y);
        }

        public static Vector4 GetVector4(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float x = 0, y = 0, z = 0, w = 0;
            LuaAPI.tolua_getfloat4(L, luaMgr.unpackVec4, stackPos, ref x, ref y, ref z, ref w);
            return new Vector4(x, y, z, w);
        }

        public static void Push(IntPtr L, Vector4 v4)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.tolua_pushfloat4(L, luaMgr.packVec4, v4.x, v4.y, v4.z, v4.w);
        }

        public static void Push(IntPtr L, RaycastHit hit)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            luaMgr.packRaycastHit.push(L);

            Push(L, hit.collider);
            Push(L, hit.distance);
            Push(L, hit.normal);
            Push(L, hit.point);
            Push(L, hit.rigidbody);
            Push(L, hit.transform);

            LuaAPI.lua_pcall(L, 6, -1, 0);
        }

        public static void Push(IntPtr L, Ray ray)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.xlua_rawgeti(L, LuaAPI.LUA_REGISTRYINDEX, luaMgr.packRay);
            LuaAPI.tolua_pushfloat3(L, luaMgr.packVec3, ray.direction.x, ray.direction.y, ray.direction.z);
            LuaAPI.tolua_pushfloat3(L, luaMgr.packVec3, ray.origin.x, ray.origin.y, ray.origin.z);
            LuaAPI.lua_pcall(L, 2, -1, 0);
        }

        public static Ray GetRay(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float x = 0, y = 0, z = 0;
            float x1 = 0, y1 = 0, z1 = 0;
            LuaAPI.tolua_getfloat6(L, luaMgr.unpackRay, stackPos, ref x, ref y, ref z, ref x1, ref y1, ref z1);
            Vector3 origin = new Vector3(x, y, z);
            Vector3 dir = new Vector3(x1, y1, z1);
            return new Ray(origin, dir);
        }

        public static Bounds GetBounds(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float x = 0, y = 0, z = 0;
            float x1 = 0, y1 = 0, z1 = 0;
            LuaAPI.tolua_getfloat6(L, luaMgr.unpackBounds, stackPos, ref x, ref y, ref z, ref x1, ref y1, ref z1);
            Vector3 center = new Vector3(x, y, z);
            Vector3 size = new Vector3(x1, y1, z1);
            return new Bounds(center, size);
        }

        public static Color GetColor(IntPtr L, int stackPos)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            float r = 0, g = 0, b = 0, a = 0;
            LuaAPI.tolua_getfloat4(L, luaMgr.unpackColor, stackPos, ref r, ref g, ref b, ref a);
            return new Color(r, g, b, a);
        }

        public static void Push(IntPtr L, Color clr)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.tolua_pushfloat4(L, luaMgr.packColor, clr.r, clr.g, clr.b, clr.a);
        }

        public static void Push(IntPtr L, Touch touch)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            luaMgr.packTouch.push(L);

            LuaAPI.lua_pushinteger(L, touch.fingerId);
            LuaAPI.tolua_pushfloat2(L, luaMgr.packVec2, touch.position.x, touch.position.y);
            LuaAPI.tolua_pushfloat2(L, luaMgr.packVec2, touch.rawPosition.x, touch.rawPosition.y);
            LuaAPI.tolua_pushfloat2(L, luaMgr.packVec2, touch.deltaPosition.x, touch.deltaPosition.y);
            LuaAPI.lua_pushnumber(L, touch.deltaTime);
            LuaAPI.lua_pushinteger(L, touch.tapCount);
            LuaAPI.lua_pushinteger(L, (int)touch.phase);

            LuaAPI.lua_pcall(L, 7, -1,0);
        }

        public static void Push(IntPtr L, Bounds bound)
        {
            LuaScriptMgr luaMgr = GetMgrFromLuaState(L);
            LuaAPI.xlua_rawgeti(L, LuaAPI.LUA_REGISTRYINDEX, luaMgr.packBounds);

            Push(L, bound.center);
            Push(L, bound.size);

            LuaAPI.lua_pcall(L, 2, -1, 0);
        }

        public static void PushTraceBack(IntPtr L)
        {
            if (traceback == null)
            {
                LuaAPI.lua_getglobal(L, "traceback");
                return;
            }
            traceback.push();
        }
    }
}