#ifdef HLSL

// Transform
float4x4 u_worldMatrix[MAX_INSTANCES_COUNT];
float4x4 u_worldViewProjectionMatrix[MAX_INSTANCES_COUNT];

// Material
float4 u_materialColor;

// Emission color
float4 u_emissionColor;

// Light
float3 u_ambientLightColor;
float3 u_diffuseLightColor1;
float3 u_directionToLight1;
float3 u_diffuseLightColor2;
float3 u_directionToLight2;

// Fog

void main(
	in float3 a_position: POSITION,
	in float3 a_normal: NORMAL,
	in float2 a_texcoord: TEXCOORD,
	in float a_instance: INSTANCE,
	out float4 v_color : COLOR,
	out float2 v_texcoord : TEXCOORD,
    out float3 v_viewPosition : TEXCOORD1,
    out float4 v_skyPosition : TEXCOORD2,
	out float4 sv_position: SV_POSITION
)
{
	// Texture
	v_texcoord = a_texcoord;
	
	// Instancing
	int instance = int(a_instance);

	// Normal
	float3 worldNormal = normalize(mul(float4(a_normal, 0.0), u_worldMatrix[instance]).xyz);

	// Lighting
	v_color = u_materialColor;
	float3 lightColor = u_ambientLightColor;
	lightColor += u_diffuseLightColor1 * max(dot(u_directionToLight1, worldNormal), 0.0);
	lightColor += u_diffuseLightColor2 * max(dot(u_directionToLight2, worldNormal), 0.0);
	v_color = float4(lightColor, 1) * u_materialColor;

	// Emission color
	v_color += u_emissionColor;
	
	// Fog
    v_viewPosition = mul(float4(a_position, 1.0), u_worldMatrix[instance]).xyz;

	// Position
	sv_position = mul(float4(a_position, 1.0), u_worldViewProjectionMatrix[instance]);
    v_skyPosition = sv_position;
}

#endif
#ifdef GLSL

// <Semantic Name='POSITION' Attribute='a_position' />
// <Semantic Name='NORMAL' Attribute='a_normal' />
// <Semantic Name='COLOR' Attribute='a_color' />
// <Semantic Name='TEXCOORD' Attribute='a_texcoord' />
// <Semantic Name='INSTANCE' Attribute='a_instance' />

// Transform
uniform mat4 u_worldMatrix[MAX_INSTANCES_COUNT];
uniform mat4 u_worldViewProjectionMatrix[MAX_INSTANCES_COUNT];

// Material
uniform vec4 u_materialColor;

// Emission color
uniform vec4 u_emissionColor;

// Light
uniform vec3 u_ambientLightColor;
uniform vec3 u_diffuseLightColor1;
uniform vec3 u_directionToLight1;
uniform vec3 u_diffuseLightColor2;
uniform vec3 u_directionToLight2;

// Fog

// Inputs and outputs
attribute vec3 a_position;
attribute vec3 a_normal;
attribute vec2 a_texcoord;
attribute float a_instance;
varying vec4 v_color;
varying vec2 v_texcoord;
#ifdef GL_ES
varying highp vec3 v_viewPosition;
varying highp vec4 v_skyPosition;
#else
varying vec3 v_viewPosition;
varying vec4 v_skyPosition;
#endif

void main()
{
	// Texture
	v_texcoord = a_texcoord;
	
	// Instancing
	int instance = int(a_instance);
	
	// Normal
	vec3 worldNormal = normalize(vec3(u_worldMatrix[instance] * vec4(a_normal, 0.0)));

	// Lighting
	v_color = u_materialColor;
	vec3 lightColor = u_ambientLightColor;
	lightColor += u_diffuseLightColor1 * max(dot(u_directionToLight1, worldNormal), 0.0);
	lightColor += u_diffuseLightColor2 * max(dot(u_directionToLight2, worldNormal), 0.0);
	v_color = vec4(lightColor, 1) * u_materialColor;

	// Emission color
	v_color += u_emissionColor;

	// Fog
    v_viewPosition = (u_worldMatrix[instance] * vec4(a_position, 1.0)).xyz;
	
	// Position
	gl_Position = u_worldViewProjectionMatrix[instance] * vec4(a_position, 1.0);
    v_skyPosition = gl_Position;

	// Fix gl_Position
	OPENGL_POSITION_FIX;
}

#endif
