#ifndef __itable__
#define __itable__

#include <string>
#include <iostream>
#include "lua.hpp"
#include "common.h"


/*
 �����API������˵ main(), c# etc.��
 
 �ⲿ��Ҫ��lua������·�������bytes���ڵ�·��

 flag: c++�ڲ�main()��0�� unity��1
*/


void add_search_path(lua_State *L, std::string path);

int inner_load(lua_State* L, const char* search_path, const char* table_path, unsigned char flag);

extern "C" 
{

	LUA_API int luaE_table(lua_State* L, const char* search_path, const char* table_path, unsigned char flag);

}

#endif
