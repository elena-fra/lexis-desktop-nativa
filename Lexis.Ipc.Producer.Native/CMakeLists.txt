cmake_minimum_required(VERSION 3.20)
project(LexisIpcProducerNative LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Prefer vcpkg toolchain if provided; otherwise try find_package / FetchContent.
option(LEXIS_FETCH_ZMQ "Fetch libzmq+cppzmq via FetchContent if not found" ON)

find_package(cppzmq CONFIG QUIET)
find_package(ZeroMQ CONFIG QUIET)

if (NOT cppzmq_FOUND AND NOT ZeroMQ_FOUND AND LEXIS_FETCH_ZMQ)
  include(FetchContent)
  # CMake 4.x rejects libzmq's old cmake_minimum_required (< 3.5).
  set(CMAKE_POLICY_VERSION_MINIMUM 3.5)
  set(BUILD_SHARED ON CACHE BOOL "" FORCE)
  set(BUILD_STATIC OFF CACHE BOOL "" FORCE)
  set(BUILD_TESTS OFF CACHE BOOL "" FORCE)
  set(ENABLE_DRAFTS OFF CACHE BOOL "" FORCE)
  set(WITH_PERF_TOOL OFF CACHE BOOL "" FORCE)
  set(ZMQ_BUILD_TESTS OFF CACHE BOOL "" FORCE)

  FetchContent_Declare(
    libzmq
    GIT_REPOSITORY https://github.com/zeromq/libzmq.git
    GIT_TAG        v4.3.5
    GIT_SHALLOW    TRUE)
  FetchContent_MakeAvailable(libzmq)

  FetchContent_Declare(
    cppzmq
    GIT_REPOSITORY https://github.com/zeromq/cppzmq.git
    GIT_TAG        v4.10.0
    GIT_SHALLOW    TRUE)
  set(CPPZMQ_BUILD_TESTS OFF CACHE BOOL "" FORCE)
  FetchContent_MakeAvailable(cppzmq)
endif()

add_executable(Lexis.Ipc.Producer.Native
  src/main.cpp)

target_include_directories(Lexis.Ipc.Producer.Native PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}/include)

if (TARGET cppzmq)
  target_link_libraries(Lexis.Ipc.Producer.Native PRIVATE cppzmq)
elseif (TARGET libzmq)
  target_link_libraries(Lexis.Ipc.Producer.Native PRIVATE libzmq)
  target_include_directories(Lexis.Ipc.Producer.Native PRIVATE
    ${cppzmq_SOURCE_DIR})
elseif (TARGET libzmq-static)
  target_compile_definitions(Lexis.Ipc.Producer.Native PRIVATE ZMQ_STATIC)
  target_link_libraries(Lexis.Ipc.Producer.Native PRIVATE libzmq-static)
else()
  # vcpkg classic find
  find_path(CPPZMQ_INCLUDE_DIR zmq.hpp)
  find_library(ZMQ_LIBRARY NAMES libzmq zmq libzmq-v143-mt-4_3_5 libzmq-mt-4_3_5)
  if (NOT CPPZMQ_INCLUDE_DIR OR NOT ZMQ_LIBRARY)
    message(FATAL_ERROR "cppzmq/libzmq not found. Install via vcpkg or enable LEXIS_FETCH_ZMQ.")
  endif()
  target_include_directories(Lexis.Ipc.Producer.Native PRIVATE ${CPPZMQ_INCLUDE_DIR})
  target_link_libraries(Lexis.Ipc.Producer.Native PRIVATE ${ZMQ_LIBRARY})
endif()

if (WIN32)
  target_compile_definitions(Lexis.Ipc.Producer.Native PRIVATE _CRT_SECURE_NO_WARNINGS)
endif()

install(TARGETS Lexis.Ipc.Producer.Native RUNTIME DESTINATION bin)
