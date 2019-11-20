#include "xlua.h"

xlua::xlua()
{
	file = "main.lua";
	tag = "xlua";
}


void xlua::exec(lua_State* L)
{
	lua_settop(L, 0);// ���ջ
	luaL_dofile(L, file);
	luaL_dostring(L, "print(\"called in cpp\")");

	//��ȡLUA�б���  
	lua_getglobal(L, "str");
	string str = lua_tostring(L, -1);
	cout << "str = " << str.c_str() << endl;
	LUAPRINT(tag);

	//��ȡtable  
	lua_getglobal(L, "tbl");
	lua_getfield(L, -1, "id");
	lua_getfield(L, -2, "name");
	lua_getfield(L, -3, "tb2");
	LUAPRINT(tag);

	//��ȡ����
	lua_getglobal(L, "add");        // ��ȡ������ѹ��ջ��  
	lua_pushnumber(L, 10);          // ѹ���һ������  
	lua_pushnumber(L, 20);          // ѹ��ڶ�������  
	int iRet = lua_pcall(L, 2, 1, 0);// ���ú�������������Ժ󣬻Ὣ����ֵѹ��ջ�У�2��ʾ����������1��ʾ���ؽ��������  
	if (iRet)                       // ���ó���  
	{
		const char *pErrorMsg = lua_tostring(L, -1);
		cout << pErrorMsg << endl;
	}
	if (lua_isnumber(L, -1))        //ȡֵ���  
	{
		cout << lua_type(L, 0) << " xx " << lua_type(L, -1) << endl;
		double fValue = lua_tonumber(L, -1);
		cout << "Result is " << fValue << endl;
		
	}
	LUAPRINT(tag);
}