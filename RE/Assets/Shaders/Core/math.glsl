#define PI 3.14159265358979323846
#define TWO_PI 6.28318530717958647692
#define HALF_PI 1.57079632679489661923

float saturate(float x) { return clamp(x, 0.0, 1.0); }
vec2  saturate(vec2 x)   { return clamp(x, 0.0, 1.0); }
vec3  saturate(vec3 x)   { return clamp(x, 0.0, 1.0); }

float sqr(float x) { return x * x; }
float cube(float x) { return x * x * x; }

float signNotZero(float v) { return v >= 0.0 ? 1.0 : -1.0; }
vec2  signNotZero(vec2 v)  { return vec2(signNotZero(v.x), signNotZero(v.y)); }
vec3  signNotZero(vec3 v)  { return vec3(signNotZero(v.x), signNotZero(v.y), signNotZero(v.z)); }

float safeDivide(float a, float b) { return a / max(b, 1e-8); }

float luminance(vec3 c) {
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

float inverseLerp(float a, float b, float v) {
    return (v - a) / (b - a);
}

float remap(float v, float inA, float inB, float outA, float outB) {
    return outA + (v - inA) * (outB - outA) / (inB - inA);
}

float smooth01(float x) {
    x = saturate(x);
    return x * x * (3.0 - 2.0 * x);
}

float smoother01(float x) {
    x = saturate(x);
    return x*x*x*(x*(x*6.0 - 15.0) + 10.0);
}

mat2 rot2(float a) {
    float s = sin(a), c = cos(a);
    return mat2(c, -s, s, c);
}

mat3 rotX(float a) {
    float s = sin(a), c = cos(a);
    return mat3(
    1, 0, 0,
    0, c, -s,
    0, s, c
    );
}

mat3 rotY(float a) {
    float s = sin(a), c = cos(a);
    return mat3(
    c, 0, s,
    0, 1, 0,
    -s, 0, c
    );
}

mat3 rotZ(float a) {
    float s = sin(a), c = cos(a);
    return mat3(
    c, -s, 0,
    s, c, 0,
    0, 0, 1
    );
}

float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash12(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

vec2 hash22(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

float valueNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);

    float a = hash12(i);
    float b = hash12(i + vec2(1, 0));
    float c = hash12(i + vec2(0, 1));
    float d = hash12(i + vec2(1, 1));

    vec2 u = f*f*(3.0 - 2.0*f);

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;

    for (int i = 0; i < 5; i++) {
        v += a * valueNoise(p);
        p *= 2.0;
        a *= 0.5;
    }
    return v;
}

float pointLineDist(vec3 p, vec3 a, vec3 b) {
    vec3 pa = p - a;
    vec3 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}

vec3 project(vec3 a, vec3 b) {
    return dot(a, b) / dot(b, b) * b;
}

vec3 reject(vec3 a, vec3 b) {
    return a - project(a, b);
}

float smoothMin(float a, float b, float k) {
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h*h*h*k*(1.0/6.0);
}

float smoothMax(float a, float b, float k) {
    return -smoothMin(-a, -b, k);
}

float fresnelSchlick(float cosTheta, float power) {
    return pow(1.0 - cosTheta, power);
}

vec3 fresnelSchlick(vec3 F0, float cosTheta) {
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

vec3 gammaCorrect(vec3 c) {
    return pow(c, vec3(1.0 / 2.2));
}

vec3 linearize(vec3 c) {
    return pow(c, vec3(2.2));
}

vec3 rgb2hsv(vec3 c) {
    vec4 K = vec4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;

    return vec3(abs(q.z + (q.w - q.y) / (6.0*d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
    vec3 p = abs(fract(c.xxx + vec3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

float linearizeDepth(float d, float n, float f) {
    float z = d * 2.0 - 1.0;
    return (2.0 * n * f) / (f + n - z * (f - n));
}