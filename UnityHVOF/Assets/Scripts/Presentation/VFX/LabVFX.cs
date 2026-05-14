// ============================================================================
// VFX — Enhanced Laser Beam + Sparks + Smoke + Gas Flow visual effects.
//
// LaserBeamVFX: Volumetric beam, multi-layer impact sparks, smoke wisps,
//               impact glow with heat color, all linked to LaserState.
//
// GasFlowVFX: Argon flow particles with volumetric cloud appearance.
// ============================================================================

using UnityEngine;
using HVOFSim.Domain.Entities;
using HVOFSim.Domain.ValueObjects;
using Material = UnityEngine.Material;

namespace HVOFSim.Presentation.VFX
{
    public sealed class LaserBeamVFX : MonoBehaviour
    {
        private LineRenderer _beam;
        private LineRenderer _beamMid;
        private LineRenderer _beamCore;
        private ParticleSystem _impactSparks;
        private ParticleSystem _smokeWisps;
        private ParticleSystem _impactGlow;
        private Light _impactLight;
        private Transform _laserHead;
        private Transform _workpiece;

        // Animation Targets
        private float _targetOuterWidth;
        private float _targetMidWidth;
        private float _targetCoreWidth;
        private float _targetLightIntensity;

        public void Initialize(Transform laserHead, Transform workpiece, Light impactLight)
        {
            _laserHead = laserHead;
            _workpiece = workpiece;
            _impactLight = impactLight;

            // Outer beam (wide orange volumetric halo)
            var beamGO = new GameObject("LaserBeamOuter");
            beamGO.transform.SetParent(transform);
            _beam = beamGO.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.material = CreateBeamMaterial(new Color(1f, 0.2f, 0.0f, 0.15f)); // Very transparent orange
            _beam.enabled = false;

            // Mid beam (bright yellow/orange heat)
            var midGO = new GameObject("LaserBeamMid");
            midGO.transform.SetParent(transform);
            _beamMid = midGO.AddComponent<LineRenderer>();
            _beamMid.positionCount = 2;
            _beamMid.material = CreateBeamMaterial(new Color(1f, 0.6f, 0.1f, 0.7f));
            _beamMid.enabled = false;

            // Inner beam core (scorching white-hot)
            var coreGO = new GameObject("LaserBeamCore");
            coreGO.transform.SetParent(transform);
            _beamCore = coreGO.AddComponent<LineRenderer>();
            _beamCore.positionCount = 2;
            _beamCore.material = CreateBeamMaterial(new Color(1f, 0.95f, 0.8f, 1f));
            _beamCore.enabled = false;

            // Impact sparks — orange/yellow
            _impactSparks = CreateParticleSystem("ImpactSparks");
            ConfigureSparks(_impactSparks);

            // Smoke wisps — rising gray cloud
            _smokeWisps = CreateParticleSystem("SmokeWisps");
            ConfigureSmoke(_smokeWisps);

            // Impact glow — bright expanding ring
            _impactGlow = CreateParticleSystem("ImpactGlow");
            ConfigureImpactGlow(_impactGlow);
        }

        public void UpdateState(LaserState state, double powerPct)
        {
            if (_beam == null || _beamMid == null || _beamCore == null) return;

            bool isFiring = state == LaserState.Firing;
            _beam.enabled = isFiring;
            _beamMid.enabled = isFiring;
            _beamCore.enabled = isFiring;

            if (isFiring && _laserHead != null && _workpiece != null)
            {
                Vector3 start = _laserHead.position + Vector3.down * 0.1f;
                // Add tiny offset to end to prevent Z-fighting at exactly surface level if there's any
                Vector3 end = new Vector3(start.x, _workpiece.position.y, start.z);

                _beam.SetPosition(0, start);
                _beam.SetPosition(1, end);
                _beamMid.SetPosition(0, start);
                _beamMid.SetPosition(1, end);
                _beamCore.SetPosition(0, start);
                _beamCore.SetPosition(1, end);

                float pct = (float)(powerPct / 100.0);
                
                // Set targets for animation — thin beam for realistic fiber laser
                _targetOuterWidth = 0.012f + pct * 0.02f;
                _targetMidWidth = 0.005f + pct * 0.01f;
                _targetCoreWidth = 0.002f + pct * 0.004f;

                Vector3 impactPos = end;
                _impactSparks.transform.position = impactPos;
                _smokeWisps.transform.position = impactPos + Vector3.up * 0.05f;
                _impactGlow.transform.position = impactPos;

                if (!_impactSparks.isPlaying) _impactSparks.Play();
                if (!_smokeWisps.isPlaying) _smokeWisps.Play();
                if (!_impactGlow.isPlaying) _impactGlow.Play();

                // Exploding massive amounts of sparks and intense glow on high power
                SetEmissionRate(_impactSparks, 150f + pct * 800f);
                SetEmissionRate(_smokeWisps, 20f + pct * 100f);
                SetEmissionRate(_impactGlow, 20f + pct * 50f);

                if (_impactLight != null)
                {
                    _impactLight.range = 2.0f + pct * 3.0f;
                    _targetLightIntensity = pct * 15f; // Intense light
                }
            }
            else
            {
                if (_impactSparks != null && _impactSparks.isPlaying) _impactSparks.Stop();
                if (_smokeWisps != null && _smokeWisps.isPlaying) _smokeWisps.Stop();
                if (_impactGlow != null && _impactGlow.isPlaying) _impactGlow.Stop();
                if (_impactLight != null) _impactLight.intensity = 0f;
            }
        }

        private void Update()
        {
            if (_beam != null && _beamMid != null && _beamCore != null && _beam.enabled)
            {
                // Add fast, chaotic noise to beam widths to simulate raw plasma energy
                float time = Time.time;
                float noiseOuter = Mathf.PerlinNoise(time * 35f, 0f) * 0.006f;
                float noiseMid = Mathf.PerlinNoise(time * 45f, 100f) * 0.003f;
                float noiseCore = Mathf.PerlinNoise(time * 55f, 200f) * 0.0015f;

                _beam.startWidth = _targetOuterWidth + noiseOuter;
                _beam.endWidth = _beam.startWidth * 1.15f;

                _beamMid.startWidth = _targetMidWidth + noiseMid;
                _beamMid.endWidth = _beamMid.startWidth * 1.1f;

                _beamCore.startWidth = _targetCoreWidth + noiseCore;
                _beamCore.endWidth = _beamCore.startWidth * 1.05f;

                if (_impactLight != null)
                {
                    // Intense power flicker for the point light
                    _impactLight.intensity = _targetLightIntensity + Random.Range(-2f, 2f);
                }
            }
        }


        private void ConfigureSparks(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f); // Much faster
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f); // Bigger sparks
            main.maxParticles = 3000;
            main.startColor = new Color(1f, 0.9f, 0.4f, 1f); // Brighter white/yellow
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 4f;

            var emission = ps.emission;
            emission.rateOverTime = 800f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 65f;
            shape.radius = 0.05f;

            var colorOL = ps.colorOverLifetime;
            colorOL.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.4f),
                    new GradientColorKey(new Color(0.5f, 0.1f, 0.0f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOL.color = gradient;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.1f));
        }

        private void ConfigureSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.maxParticles = 400;
            main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.5f; // Rise upward faster

            var emission = ps.emission;
            emission.rateOverTime = 60f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 3f));

            var colorOL = ps.colorOverLifetime;
            colorOL.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.6f, 0.5f, 0.4f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.2f, 0f),
                    new GradientAlphaKey(0.08f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOL.color = g;
        }

        private void ConfigureImpactGlow(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = 0.35f; // Huge glow base
            main.maxParticles = 8;
            main.startColor = new Color(1f, 0.8f, 0.3f, 0.8f); // Brighter and more opaque
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 25f;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.0f));

            var colorOL = ps.colorOverLifetime;
            colorOL.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOL.color = g;
        }

        private ParticleSystem CreateParticleSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            ps.Stop();
            return ps;
        }

        private Material CreateBeamMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = color;
            return mat;
        }

        private void SetEmissionRate(ParticleSystem ps, float rate)
        {
            var em = ps.emission;
            em.rateOverTime = rate;
        }

        /// <summary>Update beam colors based on laser type wavelength.</summary>
        public void UpdateBeamColors(LaserType type)
        {
            Color outer, mid, core;
            switch (type)
            {
                case LaserType.CO2:
                    // 10.6 µm — invisible IR, rendered as deep red glow
                    outer = new Color(0.8f, 0.05f, 0.0f, 0.15f);
                    mid = new Color(1f, 0.15f, 0.05f, 0.7f);
                    core = new Color(1f, 0.4f, 0.2f, 1f);
                    break;
                case LaserType.NdYAG:
                    // 1064 nm — near IR, rendered as orange-yellow
                    outer = new Color(1f, 0.3f, 0.0f, 0.15f);
                    mid = new Color(1f, 0.6f, 0.1f, 0.7f);
                    core = new Color(1f, 0.9f, 0.6f, 1f);
                    break;
                case LaserType.Diode:
                    // 940 nm — near IR, rendered as warm red-orange
                    outer = new Color(0.9f, 0.15f, 0.0f, 0.15f);
                    mid = new Color(1f, 0.35f, 0.05f, 0.7f);
                    core = new Color(1f, 0.7f, 0.4f, 1f);
                    break;
                default: // YtterbiumFiber — 1070 nm — default orange
                    outer = new Color(1f, 0.2f, 0.0f, 0.15f);
                    mid = new Color(1f, 0.6f, 0.1f, 0.7f);
                    core = new Color(1f, 0.95f, 0.8f, 1f);
                    break;
            }
            if (_beam != null && _beam.material != null) _beam.material.color = outer;
            if (_beamMid != null && _beamMid.material != null) _beamMid.material.color = mid;
            if (_beamCore != null && _beamCore.material != null) _beamCore.material.color = core;
        }
    }

    public sealed class GasFlowVFX : MonoBehaviour
    {
        private ParticleSystem _gasParticles;
        private ParticleSystem _gasMist;

        public void Initialize(Vector3 nozzlePosition)
        {
            // Main gas stream
            var go = new GameObject("GasFlowParticles");
            go.transform.SetParent(transform);
            go.transform.position = nozzlePosition;
            go.transform.rotation = Quaternion.Euler(90, 0, 0); // Spray downwards
            _gasParticles = go.AddComponent<ParticleSystem>();
            var psr = _gasParticles.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            ConfigureGasStream(_gasParticles);
            _gasParticles.Stop();

            // Ambient gas mist (volumetric cloud feel)
            var mistGO = new GameObject("GasMist");
            mistGO.transform.SetParent(transform);
            mistGO.transform.position = nozzlePosition + Vector3.down * 0.3f;
            _gasMist = mistGO.AddComponent<ParticleSystem>();
            var mistR = _gasMist.GetComponent<ParticleSystemRenderer>();
            mistR.material = new Material(Shader.Find("Sprites/Default"));
            ConfigureGasMist(_gasMist);
            _gasMist.Stop();
        }

        public void UpdateState(GasState state, double flowLPerMin)
        {
            if (_gasParticles == null || _gasMist == null) return;

            if (state == GasState.Closed)
            {
                if (_gasParticles.isPlaying) _gasParticles.Stop();
                if (_gasMist.isPlaying) _gasMist.Stop();
                return;
            }

            if (!_gasParticles.isPlaying) _gasParticles.Play();
            if (!_gasMist.isPlaying) _gasMist.Play();

            float flow = (float)flowLPerMin;
            var em = _gasParticles.emission;
            var mistEm = _gasMist.emission;
            var main = _gasParticles.main;
            
            if (state == GasState.Purging)
            {
                // Huge burst of gas when purging
                em.rateOverTime = flow * 30f;
                mistEm.rateOverTime = flow * 25f;
                main.startSpeed = 0.3f + flow * 0.1f;
            }
            else
            {
                // Normal flow
                em.rateOverTime = flow * 12f;
                mistEm.rateOverTime = flow * 5f;
                main.startSpeed = 0.15f + flow * 0.06f;
            }
        }

        private void ConfigureGasStream(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.maxParticles = 400;
            main.startColor = new Color(0.6f, 0.75f, 1f, 0.12f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.03f;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 2.5f));

            var colorOL = ps.colorOverLifetime;
            colorOL.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.8f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.6f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(0.15f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOL.color = g;
        }

        private void ConfigureGasMist(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.maxParticles = 100;
            main.startColor = new Color(0.6f, 0.7f, 1f, 0.05f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2f));

            var colorOL = ps.colorOverLifetime;
            colorOL.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.7f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.6f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(0.06f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOL.color = g;
        }

        /// <summary>Tint gas particles based on gas type.</summary>
        public void UpdateGasColor(GasType type)
        {
            Color gasColor;
            switch (type)
            {
                case GasType.Argon:
                    gasColor = new Color(0.5f, 0.7f, 1f, 0.12f); // Light blue
                    break;
                case GasType.Nitrogen:
                    gasColor = new Color(0.6f, 0.6f, 0.6f, 0.15f); // Grey
                    break;
                case GasType.Helium:
                    gasColor = new Color(0.9f, 0.85f, 0.6f, 0.10f); // Pale yellow
                    break;
                case GasType.ArgonHydrogenMix:
                    gasColor = new Color(0.4f, 0.8f, 0.6f, 0.12f); // Greenish
                    break;
                default: // None
                    return;
            }
            if (_gasParticles != null)
            {
                var main = _gasParticles.main;
                main.startColor = gasColor;
            }
            if (_gasMist != null)
            {
                var main = _gasMist.main;
                main.startColor = new Color(gasColor.r, gasColor.g, gasColor.b, gasColor.a * 0.5f);
            }
        }
    }
}
