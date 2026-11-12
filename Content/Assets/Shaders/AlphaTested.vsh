#ifdef HLSL

float2 u_origin;
float4x4 u_viewProjectionMatrix;
float3 u_viewPosition;

#ifdef TERRAIN_LIGHT_RESPONSE
float2 u_lightEnvironment;

float calculateLight(float4 levels0, float4 levels1, float4 levels2, float4 levels3)
{
    float4 levels = levels0;
    if (u_lightEnvironment.x >= 4.0) levels = levels1;
    if (u_lightEnvironment.x >= 8.0) levels = levels2;
    if (u_lightEnvironment.x >= 12.0) levels = levels3;
    float component = u_lightEnvironment.x - 4.0 * floor(u_lightEnvironment.x / 4.0);
    float response = levels.x;
    if (component >= 1.0) response = levels.y;
    if (component >= 2.0) response = levels.z;
    if (component >= 3.0) response = levels.w;
    return lerp(u_lightEnvironment.y, 1.0, response);
}
#endif

void main(
	in float3 a_position: POSITION,
	in float4 a_color: COLOR,
	in float2 a_texcoord: TEXCOORD,
#ifdef TERRAIN_LIGHT_RESPONSE
    in float4 a_light0 : TEXCOORD1,
    in float4 a_light1 : TEXCOORD2,
    in float4 a_light2 : TEXCOORD3,
    in float4 a_light3 : TEXCOORD4,
#endif
	out float4 v_color : COLOR,
	out float2 v_texcoord : TEXCOORD,
    out float3 v_viewOffset : TEXCOORD2,
    out float4 v_skyPosition : TEXCOORD1,
	out float4 sv_position: SV_POSITION
)
{
	// Texture
	v_texcoord = a_texcoord;

	// Vertex color
	v_color = a_color;
#ifdef TERRAIN_LIGHT_RESPONSE
    v_color.rgb *= calculateLight(a_light0, a_light1, a_light2, a_light3);
#endif
	
    v_viewOffset = a_position - u_viewPosition;
	
	// Position
	sv_position = mul(float4(a_position.x - u_origin.x, a_position.y, a_position.z - u_origin.y, 1.0), u_viewProjectionMatrix);
    v_skyPosition = sv_position;
}

#endif
#ifdef GLSL

// <Semantic Name='POSITION' Attribute='a_position' />
// <Semantic Name='COLOR' Attribute='a_color' />
// <Semantic Name='TEXCOORD' Attribute='a_texcoord' />

uniform vec2 u_origin;
uniform mat4 u_viewProjectionMatrix;
uniform vec3 u_viewPosition;

#ifdef TERRAIN_LIGHT_RESPONSE
uniform vec2 u_lightEnvironment;

float calculateLight(vec4 levels0, vec4 levels1, vec4 levels2, vec4 levels3)
{
    vec4 levels = levels0;
    if (u_lightEnvironment.x >= 4.0) levels = levels1;
    if (u_lightEnvironment.x >= 8.0) levels = levels2;
    if (u_lightEnvironment.x >= 12.0) levels = levels3;
    float component = u_lightEnvironment.x - 4.0 * floor(u_lightEnvironment.x / 4.0);
    float response = levels.x;
    if (component >= 1.0) response = levels.y;
    if (component >= 2.0) response = levels.z;
    if (component >= 3.0) response = levels.w;
    return mix(u_lightEnvironment.y, 1.0, response);
}
#endif

attribute vec3 a_position;
attribute vec4 a_color;
attribute vec2 a_texcoord;
#ifdef TERRAIN_LIGHT_RESPONSE
// <Semantic Name='TEXCOORD1' Attribute='a_light0' />
// <Semantic Name='TEXCOORD2' Attribute='a_light1' />
// <Semantic Name='TEXCOORD3' Attribute='a_light2' />
// <Semantic Name='TEXCOORD4' Attribute='a_light3' />
attribute vec4 a_light0;
attribute vec4 a_light1;
attribute vec4 a_light2;
attribute vec4 a_light3;
#endif

varying vec4 v_color;
varying vec2 v_texcoord;
#ifdef GL_ES
varying highp vec3 v_viewOffset;
#else
varying vec3 v_viewOffset;
#endif
#ifdef GL_ES
varying highp vec4 v_skyPosition;
#else
varying vec4 v_skyPosition;
#endif

void main()
{
	// Texture
	v_texcoord = a_texcoord;

	// Vertex color
	v_color = a_color;
#ifdef TERRAIN_LIGHT_RESPONSE
    v_color.rgb *= calculateLight(a_light0, a_light1, a_light2, a_light3);
#endif
	
    v_viewOffset = a_position - u_viewPosition;
	
	// Position
	gl_Position = u_viewProjectionMatrix * vec4(a_position.x - u_origin.x, a_position.y, a_position.z - u_origin.y, 1.0);

    v_skyPosition = gl_Position;
	// Fix gl_Position
	OPENGL_POSITION_FIX;
}

#endif
