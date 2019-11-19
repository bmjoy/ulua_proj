#include "xtable.h"

/*
 ѧϰ�ο� 
 1. https://blog.csdn.net/linuxheik/article/details/18660675
 2. https://www.cnblogs.com/chevin/p/5889119.html
*/

xtable::xtable(const char* table, int row, int col, string* title)
	:cvs(table, col, row, title)
{
	tag = "xtable";
	name = "global_c_write";
	file = "table.lua";
}


xtable::~xtable()
{
	cout << "xtable deconstruct" << endl;
}


void xtable::exec()
{
	lua_pushstring(L, "hello world");

	//�ȶ�̬����һ��table
	lua_newtable(L);

	lua_pushnumber(L, 101); // �Ƚ�ֵѹ��ջ key=101
	lua_pushstring(L, "www.jb51.net"); //global_c_write[101]="www.jb51.net"
	LUAPRINT(tag);

	lua_settable(L, -3);
	lua_pushstring(L, "baidu");
	lua_pushstring(L, "www.baidu.com"); 
	lua_settable(L, -3);
	lua_setglobal(L, name);
	LUAPRINT(tag);

	//����table���ֵ
	lua_getglobal(L, name);
	lua_rawgeti(L, -1, 101);		//keyΪnumber
	lua_getfield(L, -2, "baidu");   //keyλstring
	LUAPRINT(tag);

	luaL_dofile(L, file);
	lua_getglobal(L, "prt_ctable");
	lua_pcall(L, 0, 0, 0);

	
	table();
	lua_getglobal(L, "table_v2");
	lua_pcall(L, 0, 0, 0);
}

void xtable::table()
{
	lua_settop(L, 0);
	lua_newtable(L);

	lua_newtable(L);
	push_k_v(100, "100 string");
	push_k_v(200, "200 string");
	lua_rawseti(L, -2, 0);

	lua_newtable(L);
	push_k_v(101, "101 string");
	push_k_v("abc", "baidu string");

	uint16_t* p = new uint16_t[2] {101, 102};
	push_array("arr", p, 2);
	lua_rawseti(L, -2, 1);

	lua_setglobal(L, "m_table");
}
