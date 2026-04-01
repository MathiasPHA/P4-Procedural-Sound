using System.Collections.Generic;
using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Auto-generated collision polygon data from Forest_Tileset.tsx.
    /// Vertices are normalized (0-1) relative to tile cell, Y-up.
    /// At runtime, scale by cellSize and offset by cell position.
    /// </summary>
    public static class WaterCollisionData
    {
        /// <summary>
        /// Returns collision polygon paths for a given tsx tile ID.
        /// Each path is an array of Vector2 vertices in normalized tile-local coords.
        /// Returns null if no collision data exists for the tile.
        /// </summary>
        public static Vector2[][] GetCollisionPaths(int tsxTileId)
        {
            if (_data.TryGetValue(tsxTileId, out var paths))
                return paths;
            return null;
        }

        private static readonly Dictionary<int, Vector2[][]> _data = new Dictionary<int, Vector2[][]>
        {
            { 2, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.227456f, 1.016103f), new Vector2(0.772946f, 1.016103f), new Vector2(0.772946f, 0.229068f), new Vector2(0.227456f, 0.229068f) },
                new Vector2[] { new Vector2(-0.058374f, 0.776570f), new Vector2(1.084942f, 0.776570f), new Vector2(1.084942f, 0.229065f), new Vector2(-0.058374f, 0.229065f) },
            }},
            { 3, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.334139f, 0.607488f), new Vector2(0.357428f, 0.724573f), new Vector2(0.423751f, 0.823833f), new Vector2(0.523011f, 0.890156f), new Vector2(0.640096f, 0.913446f), new Vector2(0.757181f, 0.890156f), new Vector2(0.856441f, 0.823833f), new Vector2(0.922764f, 0.724573f), new Vector2(0.946053f, 0.607489f), new Vector2(0.946053f, 0.607489f), new Vector2(0.922764f, 0.490404f), new Vector2(0.856441f, 0.391144f), new Vector2(0.757181f, 0.324821f), new Vector2(0.640096f, 0.301531f), new Vector2(0.523011f, 0.324821f), new Vector2(0.423751f, 0.391144f), new Vector2(0.357428f, 0.490404f), new Vector2(0.334139f, 0.607488f) },
                new Vector2[] { new Vector2(0.827293f, 0.812802f), new Vector2(0.804386f, 0.688013f), new Vector2(0.739154f, 0.582223f), new Vector2(0.641527f, 0.511536f), new Vector2(0.526368f, 0.486714f), new Vector2(0.411209f, 0.511536f), new Vector2(0.313582f, 0.582223f), new Vector2(0.248350f, 0.688013f), new Vector2(0.225443f, 0.812801f), new Vector2(0.248350f, 0.937590f), new Vector2(0.313582f, 1.043380f), new Vector2(0.411209f, 1.114067f), new Vector2(0.526368f, 1.138889f), new Vector2(0.641527f, 1.114067f), new Vector2(0.739154f, 1.043380f), new Vector2(0.804386f, 0.937590f) },
                new Vector2[] { new Vector2(1.254025f, 0.507850f), new Vector2(1.227824f, 0.391149f), new Vector2(1.153210f, 0.292216f), new Vector2(1.041543f, 0.226110f), new Vector2(0.909822f, 0.202897f), new Vector2(0.778102f, 0.226110f), new Vector2(0.666435f, 0.292216f), new Vector2(0.591821f, 0.391149f), new Vector2(0.565620f, 0.507849f), new Vector2(0.591821f, 0.624550f), new Vector2(0.666435f, 0.723483f), new Vector2(0.778102f, 0.789589f), new Vector2(0.909822f, 0.812802f), new Vector2(1.041543f, 0.789589f), new Vector2(1.153210f, 0.723483f), new Vector2(1.227824f, 0.624550f) },
            }},
            { 4, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.048685f, 0.610443f), new Vector2(0.071974f, 0.727528f), new Vector2(0.138297f, 0.826788f), new Vector2(0.237557f, 0.893111f), new Vector2(0.354642f, 0.916401f), new Vector2(0.471727f, 0.893111f), new Vector2(0.570987f, 0.826788f), new Vector2(0.637310f, 0.727528f), new Vector2(0.660600f, 0.610444f), new Vector2(0.660600f, 0.610443f), new Vector2(0.637310f, 0.493359f), new Vector2(0.570987f, 0.394099f), new Vector2(0.471727f, 0.327776f), new Vector2(0.354642f, 0.304486f), new Vector2(0.237557f, 0.327776f), new Vector2(0.138297f, 0.394099f), new Vector2(0.071974f, 0.493359f), new Vector2(0.048685f, 0.610443f) },
                new Vector2[] { new Vector2(0.798250f, 0.867584f), new Vector2(0.775343f, 0.742796f), new Vector2(0.710111f, 0.637005f), new Vector2(0.612484f, 0.566318f), new Vector2(0.497325f, 0.541497f), new Vector2(0.382165f, 0.566318f), new Vector2(0.284538f, 0.637005f), new Vector2(0.219306f, 0.742796f), new Vector2(0.196399f, 0.867584f), new Vector2(0.219306f, 0.992372f), new Vector2(0.284538f, 1.098163f), new Vector2(0.382165f, 1.168850f), new Vector2(0.497324f, 1.193671f), new Vector2(0.612484f, 1.168850f), new Vector2(0.710111f, 1.098163f), new Vector2(0.775343f, 0.992372f) },
                new Vector2[] { new Vector2(0.417562f, 0.516260f), new Vector2(0.391361f, 0.399560f), new Vector2(0.316747f, 0.300626f), new Vector2(0.205080f, 0.234521f), new Vector2(0.073359f, 0.211307f), new Vector2(-0.058361f, 0.234521f), new Vector2(-0.170028f, 0.300626f), new Vector2(-0.244642f, 0.399560f), new Vector2(-0.270843f, 0.516260f), new Vector2(-0.244642f, 0.632960f), new Vector2(-0.170028f, 0.731894f), new Vector2(-0.058361f, 0.797999f), new Vector2(0.073359f, 0.821213f), new Vector2(0.205080f, 0.797999f), new Vector2(0.316747f, 0.731894f), new Vector2(0.391361f, 0.632960f) },
            }},
            { 5, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.941858f, 0.410755f), new Vector2(0.920417f, 0.293317f), new Vector2(0.855666f, 0.193024f), new Vector2(0.757464f, 0.125145f), new Vector2(0.640760f, 0.100013f), new Vector2(0.523323f, 0.121455f), new Vector2(0.423030f, 0.186205f), new Vector2(0.355151f, 0.284408f), new Vector2(0.330019f, 0.401111f), new Vector2(0.330019f, 0.401111f), new Vector2(0.351461f, 0.518548f), new Vector2(0.416211f, 0.618841f), new Vector2(0.514414f, 0.686720f), new Vector2(0.631117f, 0.711852f), new Vector2(0.748554f, 0.690411f), new Vector2(0.848847f, 0.625660f), new Vector2(0.916726f, 0.527458f), new Vector2(0.941858f, 0.410755f) },
                new Vector2[] { new Vector2(0.196436f, 0.141832f), new Vector2(0.217373f, 0.266966f), new Vector2(0.280930f, 0.373771f), new Vector2(0.377431f, 0.445988f), new Vector2(0.492184f, 0.472622f), new Vector2(0.607720f, 0.449618f), new Vector2(0.706449f, 0.380478f), new Vector2(0.773341f, 0.275729f), new Vector2(0.798211f, 0.151317f), new Vector2(0.777274f, 0.026183f), new Vector2(0.713717f, -0.080622f), new Vector2(0.617216f, -0.152839f), new Vector2(0.502462f, -0.179472f), new Vector2(0.386926f, -0.156468f), new Vector2(0.288198f, -0.087329f), new Vector2(0.221306f, 0.017420f) },
                new Vector2[] { new Vector2(0.571540f, 0.499111f), new Vector2(0.595898f, 0.616209f), new Vector2(0.668943f, 0.716307f), new Vector2(0.779555f, 0.784164f), new Vector2(0.910893f, 0.809450f), new Vector2(1.042963f, 0.788315f), new Vector2(1.155659f, 0.723978f), new Vector2(1.231822f, 0.626232f), new Vector2(1.259859f, 0.509960f), new Vector2(1.235501f, 0.392861f), new Vector2(1.162455f, 0.292764f), new Vector2(1.051844f, 0.224907f), new Vector2(0.920505f, 0.199620f), new Vector2(0.788435f, 0.220755f), new Vector2(0.675740f, 0.285092f), new Vector2(0.599576f, 0.382838f) },
            }},
            { 6, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.426201f, 0.047907f), new Vector2(0.309266f, 0.071936f), new Vector2(0.210427f, 0.138885f), new Vector2(0.144732f, 0.238562f), new Vector2(0.122183f, 0.355791f), new Vector2(0.146212f, 0.472727f), new Vector2(0.213161f, 0.571566f), new Vector2(0.312838f, 0.637260f), new Vector2(0.430068f, 0.659810f), new Vector2(0.430068f, 0.659810f), new Vector2(0.547003f, 0.635781f), new Vector2(0.645842f, 0.568832f), new Vector2(0.711536f, 0.469155f), new Vector2(0.734086f, 0.351925f), new Vector2(0.710057f, 0.234990f), new Vector2(0.643108f, 0.136151f), new Vector2(0.543431f, 0.070456f), new Vector2(0.426201f, 0.047907f) },
                new Vector2[] { new Vector2(0.173802f, 0.799083f), new Vector2(0.298443f, 0.775388f), new Vector2(0.403819f, 0.709489f), new Vector2(0.473887f, 0.611417f), new Vector2(0.497981f, 0.496104f), new Vector2(0.472432f, 0.381104f), new Vector2(0.401130f, 0.283925f), new Vector2(0.294930f, 0.219362f), new Vector2(0.169999f, 0.197245f), new Vector2(0.045358f, 0.220939f), new Vector2(-0.060018f, 0.286839f), new Vector2(-0.130087f, 0.384910f), new Vector2(-0.154181f, 0.500224f), new Vector2(-0.128632f, 0.615224f), new Vector2(-0.057329f, 0.712402f), new Vector2(0.048871f, 0.776965f) },
                new Vector2[] { new Vector2(0.522714f, 0.416180f), new Vector2(0.639247f, 0.389242f), new Vector2(0.737707f, 0.314005f), new Vector2(0.803106f, 0.201922f), new Vector2(0.825486f, 0.070057f), new Vector2(0.801441f, -0.061514f), new Vector2(0.734632f, -0.172762f), new Vector2(0.635228f, -0.246749f), new Vector2(0.518365f, -0.272212f), new Vector2(0.401833f, -0.245274f), new Vector2(0.303372f, -0.170037f), new Vector2(0.237974f, -0.057954f), new Vector2(0.215593f, 0.073911f), new Vector2(0.239638f, 0.205482f), new Vector2(0.306448f, 0.316729f), new Vector2(0.405851f, 0.390717f) },
            }},
            { 7, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.818331f, 1.123487f), new Vector2(1.757971f, 0.813385f), new Vector2(1.586081f, 0.550492f), new Vector2(1.328828f, 0.374833f), new Vector2(1.025378f, 0.313150f), new Vector2(0.721929f, 0.374833f), new Vector2(0.464676f, 0.550492f), new Vector2(0.292786f, 0.813385f), new Vector2(0.232426f, 1.123487f), new Vector2(0.292786f, 1.433590f), new Vector2(0.464676f, 1.696483f), new Vector2(0.721929f, 1.872142f), new Vector2(1.025378f, 1.933825f), new Vector2(1.328828f, 1.872142f), new Vector2(1.586081f, 1.696483f), new Vector2(1.757971f, 1.433590f) },
            }},
            { 8, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.001043f, 0.997697f), new Vector2(0.994818f, 0.997697f), new Vector2(0.994818f, 0.259257f), new Vector2(0.001043f, 0.259257f) },
            }},
            { 9, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.770045f, 1.101438f), new Vector2(0.709685f, 0.791335f), new Vector2(0.537795f, 0.528442f), new Vector2(0.280542f, 0.352783f), new Vector2(-0.022907f, 0.291100f), new Vector2(-0.326357f, 0.352783f), new Vector2(-0.583610f, 0.528442f), new Vector2(-0.755500f, 0.791335f), new Vector2(-0.815860f, 1.101437f), new Vector2(-0.755500f, 1.411540f), new Vector2(-0.583610f, 1.674433f), new Vector2(-0.326357f, 1.850092f), new Vector2(-0.022908f, 1.911775f), new Vector2(0.280542f, 1.850092f), new Vector2(0.537795f, 1.674433f), new Vector2(0.709685f, 1.411540f) },
            }},
            { 10, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.219462f, 1.063478f), new Vector2(0.811407f, 1.063478f), new Vector2(0.811407f, 0.448628f), new Vector2(0.219462f, 0.448628f) },
                new Vector2[] { new Vector2(0.814792f, 0.425867f), new Vector2(0.791876f, 0.332020f), new Vector2(0.726617f, 0.252460f), new Vector2(0.628950f, 0.199299f), new Vector2(0.513744f, 0.180632f), new Vector2(0.398538f, 0.199299f), new Vector2(0.300871f, 0.252460f), new Vector2(0.235612f, 0.332020f), new Vector2(0.212697f, 0.425867f), new Vector2(0.235612f, 0.519714f), new Vector2(0.300871f, 0.599274f), new Vector2(0.398538f, 0.652435f), new Vector2(0.513744f, 0.671102f), new Vector2(0.628950f, 0.652435f), new Vector2(0.726617f, 0.599274f), new Vector2(0.791876f, 0.519714f) },
            }},
            { 11, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.077400f, 0.785335f), new Vector2(1.077400f, 0.193389f), new Vector2(0.462550f, 0.193389f), new Vector2(0.462550f, 0.785334f) },
                new Vector2[] { new Vector2(0.439820f, 0.191557f), new Vector2(0.345973f, 0.214473f), new Vector2(0.266413f, 0.279732f), new Vector2(0.213252f, 0.377399f), new Vector2(0.194585f, 0.492604f), new Vector2(0.213252f, 0.607810f), new Vector2(0.266413f, 0.705477f), new Vector2(0.345973f, 0.770736f), new Vector2(0.439820f, 0.793652f), new Vector2(0.533667f, 0.770736f), new Vector2(0.613227f, 0.705477f), new Vector2(0.666388f, 0.607810f), new Vector2(0.685055f, 0.492605f), new Vector2(0.666388f, 0.377399f), new Vector2(0.613227f, 0.279732f), new Vector2(0.533667f, 0.214473f) },
            }},
            { 12, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.070862f, 0.202615f), new Vector2(-0.068413f, 0.794555f), new Vector2(0.546431f, 0.792012f), new Vector2(0.543983f, 0.200072f) },
                new Vector2[] { new Vector2(0.569168f, 0.793755f), new Vector2(0.662920f, 0.770452f), new Vector2(0.742210f, 0.704864f), new Vector2(0.794965f, 0.606978f), new Vector2(0.813156f, 0.491696f), new Vector2(0.794012f, 0.376568f), new Vector2(0.740448f, 0.279122f), new Vector2(0.660619f, 0.214193f), new Vector2(0.566678f, 0.191666f), new Vector2(0.472926f, 0.214969f), new Vector2(0.393637f, 0.280557f), new Vector2(0.340881f, 0.378443f), new Vector2(0.322690f, 0.493725f), new Vector2(0.341834f, 0.608853f), new Vector2(0.395398f, 0.706299f), new Vector2(0.475227f, 0.771228f) },
            }},
            { 13, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.797965f, -0.053045f), new Vector2(0.206021f, -0.052136f), new Vector2(0.206965f, 0.562713f), new Vector2(0.798909f, 0.561804f) },
                new Vector2[] { new Vector2(0.205167f, 0.585444f), new Vector2(0.228227f, 0.679257f), new Vector2(0.293608f, 0.758716f), new Vector2(0.391357f, 0.811726f), new Vector2(0.506591f, 0.830217f), new Vector2(0.621768f, 0.811372f), new Vector2(0.719353f, 0.758062f), new Vector2(0.784490f, 0.678402f), new Vector2(0.807262f, 0.584520f), new Vector2(0.784202f, 0.490708f), new Vector2(0.718821f, 0.411248f), new Vector2(0.621072f, 0.358238f), new Vector2(0.505838f, 0.339747f), new Vector2(0.390661f, 0.358592f), new Vector2(0.293076f, 0.411902f), new Vector2(0.227939f, 0.491562f) },
            }},
            { 15, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.000320f, 0.999680f), new Vector2(0.998320f, 0.999680f), new Vector2(0.998320f, 0.001395f), new Vector2(0.000320f, 0.001395f) },
            }},
            { 17, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.773200f, -0.004040f), new Vector2(0.227711f, -0.003126f), new Vector2(0.229029f, 0.783908f), new Vector2(0.774519f, 0.782994f) },
                new Vector2[] { new Vector2(1.059430f, 0.235015f), new Vector2(-0.083883f, 0.236931f), new Vector2(-0.082966f, 0.784435f), new Vector2(1.060347f, 0.782519f) },
            }},
            { 18, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.001841f, 0.219315f), new Vector2(-0.001537f, 0.764805f), new Vector2(0.785498f, 0.764365f), new Vector2(0.785193f, 0.218875f) },
                new Vector2[] { new Vector2(0.237534f, -0.066645f), new Vector2(0.238173f, 1.076670f), new Vector2(0.785677f, 1.076364f), new Vector2(0.785039f, -0.066951f) },
            }},
            { 19, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.000070f, 0.772668f), new Vector2(0.997768f, 0.227183f), new Vector2(0.210740f, 0.230505f), new Vector2(0.213042f, 0.775990f) },
                new Vector2[] { new Vector2(0.761745f, 1.059505f), new Vector2(0.756920f, -0.083799f), new Vector2(0.209420f, -0.081489f), new Vector2(0.214245f, 1.061816f) },
            }},
            { 20, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.877522f, 0.432192f), new Vector2(0.848678f, 0.380114f), new Vector2(0.766536f, 0.335964f), new Vector2(0.643602f, 0.306464f), new Vector2(0.498592f, 0.296105f), new Vector2(0.353582f, 0.306464f), new Vector2(0.230648f, 0.335964f), new Vector2(0.148506f, 0.380114f), new Vector2(0.119662f, 0.432192f), new Vector2(0.148506f, 0.484270f), new Vector2(0.230648f, 0.528419f), new Vector2(0.353582f, 0.557919f), new Vector2(0.498592f, 0.568278f), new Vector2(0.643602f, 0.557919f), new Vector2(0.766536f, 0.528419f), new Vector2(0.848678f, 0.484270f) },
            }},
            { 21, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.820639f, -0.067574f), new Vector2(1.760279f, -0.377677f), new Vector2(1.588389f, -0.640569f), new Vector2(1.331136f, -0.816228f), new Vector2(1.027686f, -0.877912f), new Vector2(0.724237f, -0.816228f), new Vector2(0.466984f, -0.640569f), new Vector2(0.295094f, -0.377677f), new Vector2(0.234734f, -0.067574f), new Vector2(0.295094f, 0.242529f), new Vector2(0.466984f, 0.505421f), new Vector2(0.724237f, 0.681080f), new Vector2(1.027686f, 0.742763f), new Vector2(1.331136f, 0.681080f), new Vector2(1.588389f, 0.505421f), new Vector2(1.760279f, 0.242529f) },
            }},
            { 22, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.002878f, 0.742753f), new Vector2(0.996653f, 0.742753f), new Vector2(0.996653f, 0.004313f), new Vector2(0.002878f, 0.004313f) },
            }},
            { 23, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.762450f, -0.067574f), new Vector2(0.702090f, -0.377677f), new Vector2(0.530200f, -0.640569f), new Vector2(0.272947f, -0.816228f), new Vector2(-0.030503f, -0.877912f), new Vector2(-0.333952f, -0.816228f), new Vector2(-0.591205f, -0.640569f), new Vector2(-0.763095f, -0.377677f), new Vector2(-0.823455f, -0.067574f), new Vector2(-0.763095f, 0.242529f), new Vector2(-0.591205f, 0.505421f), new Vector2(-0.333952f, 0.681080f), new Vector2(-0.030503f, 0.742763f), new Vector2(0.272947f, 0.681080f), new Vector2(0.530200f, 0.505421f), new Vector2(0.702090f, 0.242529f) },
            }},
            { 24, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.215861f, 1.023463f), new Vector2(0.783671f, 1.023463f), new Vector2(0.783671f, -0.065227f), new Vector2(0.215861f, -0.065227f) },
                new Vector2[] { new Vector2(-0.082121f, 0.779446f), new Vector2(1.051149f, 0.779446f), new Vector2(1.051149f, 0.218677f), new Vector2(-0.082121f, 0.218677f) },
            }},
            { 25, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.056312f, 0.781793f), new Vector2(1.048803f, 0.781793f), new Vector2(1.048803f, 0.230407f), new Vector2(-0.056312f, 0.230407f) },
            }},
            { 26, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.211168f, 1.037541f), new Vector2(0.771938f, 1.037541f), new Vector2(0.771938f, -0.083999f), new Vector2(0.211168f, -0.083999f) },
            }},
            { 43, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.773150f, -0.001735f), new Vector2(0.227661f, -0.000745f), new Vector2(0.229089f, 0.786289f), new Vector2(0.774579f, 0.785299f) },
                new Vector2[] { new Vector2(1.059410f, 0.237280f), new Vector2(-0.083903f, 0.239355f), new Vector2(-0.082526f, 0.998014f), new Vector2(1.060787f, 0.995939f) },
            }},
            { 44, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.001941f, 0.223670f), new Vector2(-0.001865f, 0.769160f), new Vector2(0.785170f, 0.769050f), new Vector2(0.785094f, 0.223560f) },
                new Vector2[] { new Vector2(0.237556f, -0.062185f), new Vector2(0.237715f, 1.081130f), new Vector2(0.996375f, 1.081024f), new Vector2(0.996215f, -0.062291f) },
            }},
            { 45, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.004290f, 0.783627f), new Vector2(1.003234f, 0.238138f), new Vector2(0.216201f, 0.239661f), new Vector2(0.217256f, 0.785150f) },
                new Vector2[] { new Vector2(0.765305f, 1.069911f), new Vector2(0.763092f, -0.073402f), new Vector2(0.004433f, -0.071933f), new Vector2(0.006646f, 1.071379f) },
            }},
            { 46, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.213881f, 0.996405f), new Vector2(0.759363f, 0.999213f), new Vector2(0.763415f, 0.212189f), new Vector2(0.217933f, 0.209380f) },
                new Vector2[] { new Vector2(-0.070706f, 0.755402f), new Vector2(1.072594f, 0.761289f), new Vector2(1.076500f, 0.002639f), new Vector2(-0.066800f, -0.003247f) },
            }},
            { 47, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.199437f, 1.056312f), new Vector2(1.041767f, 1.056312f), new Vector2(1.041767f, 0.253871f), new Vector2(0.199437f, 0.253871f) },
                new Vector2[] { new Vector2(-0.093853f, 0.784139f), new Vector2(0.783668f, 0.784139f), new Vector2(0.783668f, -0.095731f), new Vector2(-0.093853f, -0.095731f) },
            }},
            { 48, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.048655f, 0.796056f), new Vector2(1.045496f, -0.046269f), new Vector2(0.243061f, -0.043259f), new Vector2(0.246221f, 0.799065f) },
                new Vector2[] { new Vector2(0.777585f, 1.090364f), new Vector2(0.774294f, 0.212850f), new Vector2(-0.105570f, 0.216150f), new Vector2(-0.102279f, 1.093664f) },
            }},
            { 50, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.244017f, 1.063351f), new Vector2(1.065227f, 1.063351f), new Vector2(1.065227f, -0.168465f), new Vector2(0.244017f, -0.168465f) },
            }},
            { 51, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.077428f, 1.084467f), new Vector2(0.701547f, 1.084467f), new Vector2(0.701547f, -0.107463f), new Vector2(-0.077428f, -0.107463f) },
            }},
            { 52, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.227593f, 1.046926f), new Vector2(1.030033f, 1.046926f), new Vector2(1.030033f, -0.025339f), new Vector2(0.227593f, -0.025339f) },
                new Vector2[] { new Vector2(-0.086814f, 0.746598f), new Vector2(1.030031f, 0.746598f), new Vector2(1.030031f, -0.025342f), new Vector2(-0.086814f, -0.025342f) },
            }},
            { 53, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.067075f, 0.723995f), new Vector2(1.064852f, -0.078442f), new Vector2(-0.007409f, -0.075472f), new Vector2(-0.005186f, 0.726965f) },
                new Vector2[] { new Vector2(0.767620f, 1.039232f), new Vector2(0.764527f, -0.077609f), new Vector2(-0.007411f, -0.075471f), new Vector2(-0.004317f, 1.041370f) },
            }},
            { 54, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.061245f, 0.258830f), new Vector2(-0.060685f, 1.061270f), new Vector2(1.011580f, 1.060521f), new Vector2(1.011020f, 0.258081f) },
                new Vector2[] { new Vector2(0.238864f, -0.055785f), new Vector2(0.239644f, 1.061060f), new Vector2(1.011584f, 1.060521f), new Vector2(1.010804f, -0.056324f) },
            }},
            { 55, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.772790f, -0.046135f), new Vector2(-0.029649f, -0.045099f), new Vector2(-0.028264f, 1.027165f), new Vector2(0.774175f, 1.026129f) },
                new Vector2[] { new Vector2(1.087580f, 0.253790f), new Vector2(-0.029264f, 0.255232f), new Vector2(-0.028267f, 1.027172f), new Vector2(1.088577f, 1.025729f) },
            }},
            { 56, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.063350f, 0.777100f), new Vector2(1.044110f, 0.777100f), new Vector2(1.044110f, 0.242140f), new Vector2(-0.063350f, 0.242140f) },
                new Vector2[] { new Vector2(0.220554f, 1.093853f), new Vector2(0.783668f, 1.093853f), new Vector2(0.783668f, -0.123888f), new Vector2(0.220554f, -0.123888f) },
                new Vector2[] { new Vector2(0.710935f, 1.093853f), new Vector2(1.044112f, 1.093853f), new Vector2(1.044112f, 0.694978f), new Vector2(0.710935f, 0.694978f) },
            }},
            { 57, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.216657f, -0.041545f), new Vector2(0.217411f, 1.065915f), new Vector2(0.787566f, 1.065527f), new Vector2(0.786812f, -0.041933f) },
                new Vector2[] { new Vector2(-0.064708f, 0.242550f), new Vector2(-0.064334f, 0.791585f), new Vector2(1.061901f, 0.790818f), new Vector2(1.061527f, 0.241783f) },
                new Vector2[] { new Vector2(-0.064375f, 0.732934f), new Vector2(-0.064148f, 1.066111f), new Vector2(0.334726f, 1.065839f), new Vector2(0.334499f, 0.732662f) },
            }},
            { 58, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.801615f, 1.058477f), new Vector2(0.800925f, -0.048983f), new Vector2(0.223730f, -0.048624f), new Vector2(0.224420f, 1.058836f) },
                new Vector2[] { new Vector2(1.118185f, 0.767338f), new Vector2(1.117839f, 0.211258f), new Vector2(-0.099901f, 0.212016f), new Vector2(-0.099555f, 0.768096f) },
                new Vector2[] { new Vector2(1.117885f, 0.283995f), new Vector2(1.117677f, -0.049182f), new Vector2(0.718803f, -0.048933f), new Vector2(0.719011f, 0.284244f) },
            }},
            { 59, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.225246f, 1.025810f), new Vector2(0.776631f, 1.025810f), new Vector2(0.776631f, -0.044110f), new Vector2(0.225246f, -0.044110f) },
                new Vector2[] { new Vector2(-0.070389f, 0.777100f), new Vector2(1.037071f, 0.777100f), new Vector2(1.037071f, 0.202255f), new Vector2(-0.070389f, 0.202255f) },
                new Vector2[] { new Vector2(-0.014078f, 0.251525f), new Vector2(0.297982f, 0.251525f), new Vector2(0.297982f, -0.004224f), new Vector2(-0.014078f, -0.004224f) },
            }},
            { 64, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.239324f, 1.021117f), new Vector2(0.790709f, 1.021117f), new Vector2(0.790709f, -0.030033f), new Vector2(0.239324f, -0.030033f) },
                new Vector2[] { new Vector2(0.741435f, 0.784139f), new Vector2(1.041764f, 0.784139f), new Vector2(1.041764f, -0.018301f), new Vector2(0.741435f, -0.018301f) },
            }},
            { 65, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.206476f, 1.028156f), new Vector2(0.757861f, 1.028156f), new Vector2(0.757861f, -0.022994f), new Vector2(0.206476f, -0.022994f) },
                new Vector2[] { new Vector2(-0.011732f, 0.765369f), new Vector2(0.288596f, 0.765369f), new Vector2(0.288596f, -0.037071f), new Vector2(-0.011732f, -0.037071f) },
            }},
            { 66, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.232286f, 1.049273f), new Vector2(0.783671f, 1.049273f), new Vector2(0.783671f, -0.001877f), new Vector2(0.232286f, -0.001877f) },
                new Vector2[] { new Vector2(0.727355f, 1.042234f), new Vector2(1.027684f, 1.042234f), new Vector2(1.027684f, 0.239794f), new Vector2(0.727355f, 0.239794f) },
            }},
            { 67, new Vector2[][]
            {
                new Vector2[] { new Vector2(0.218207f, 1.049273f), new Vector2(0.769593f, 1.049273f), new Vector2(0.769593f, -0.001877f), new Vector2(0.218207f, -0.001877f) },
                new Vector2[] { new Vector2(-0.007040f, 1.044580f), new Vector2(0.293289f, 1.044580f), new Vector2(0.293289f, 0.242140f), new Vector2(-0.007040f, 0.242140f) },
            }},
            { 68, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.007135f, 0.792859f), new Vector2(1.005207f, 0.241477f), new Vector2(-0.045936f, 0.245152f), new Vector2(-0.044009f, 0.796533f) },
                new Vector2[] { new Vector2(1.003230f, 1.018121f), new Vector2(1.002180f, 0.717794f), new Vector2(0.199745f, 0.720599f), new Vector2(0.200795f, 1.020926f) },
            }},
            { 69, new Vector2[][]
            {
                new Vector2[] { new Vector2(1.035545f, 0.777188f), new Vector2(1.034408f, 0.225804f), new Vector2(-0.016739f, 0.227970f), new Vector2(-0.015603f, 0.779354f) },
                new Vector2[] { new Vector2(1.027485f, 0.282135f), new Vector2(1.026866f, -0.018193f), new Vector2(0.224428f, -0.016539f), new Vector2(0.225047f, 0.283789f) },
            }},
            { 70, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.025595f, 0.229725f), new Vector2(-0.022631f, 0.781102f), new Vector2(1.028504f, 0.775451f), new Vector2(1.025540f, 0.224074f) },
                new Vector2[] { new Vector2(-0.015893f, 0.724747f), new Vector2(-0.014279f, 1.025072f), new Vector2(0.788150f, 1.020758f), new Vector2(0.786535f, 0.720434f) },
            }},
            { 71, new Vector2[][]
            {
                new Vector2[] { new Vector2(-0.031105f, 0.214110f), new Vector2(-0.032135f, 0.765494f), new Vector2(1.019014f, 0.767457f), new Vector2(1.020043f, 0.216073f) },
                new Vector2[] { new Vector2(-0.025992f, -0.011125f), new Vector2(-0.026553f, 0.289203f), new Vector2(0.775885f, 0.290702f), new Vector2(0.776446f, -0.009626f) },
            }},
        };
    }
}