#ifdef GLSL
// <Semantic Name='POSITION' Attribute='a_position' />
// <Semantic Name='TEXCOORD' Attribute='a_texcoord' />
uniform mat4 u_worldViewProjectionMatrix;
attribute vec3 a_position;
attribute vec2 a_texcoord;
varying vec3 v_position;
varying vec2 v_texcoord;
void main()
{
    v_position = a_position;
    v_texcoord = a_texcoord;
    gl_Position = u_worldViewProjectionMatrix * vec4(a_position, 1.0);
    OPENGL_POSITION_FIX;
}
#endif
