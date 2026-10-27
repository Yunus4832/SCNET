/* Managed entry-point bridge for Silk.NET.Windowing.Sdl's SilkActivity. */
#include <stddef.h>

typedef void (*MainCallback)(void);
static MainCallback current_main;

__attribute__((visibility("default")))
void sdSetMain(MainCallback callback)
{
    current_main = callback;
}

__attribute__((visibility("default")))
int SDL_main(int argc, char **argv)
{
    (void)argc;
    (void)argv;
    if (current_main == NULL)
        return 1;
    current_main();
    return 0;
}
