using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace LauncherX
{
    // Visual do Launcher X: portas deslizantes no topo e missil visivel no poco.
    // Nao mira e nao dispara nada: so acompanha o que o bloco vanilla ja faz.
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallMissileLauncher), false, "LauncherX")]
    public class LauncherXLogic : MyGameLogicComponent
    {
        // ---- posicoes no bloco (metros, eixos do bloco: -Z = frente/topo)
        static readonly Vector3 MissileHome = new Vector3(0f, 0f, -4.8f);      // nariz do missil no poco
        static readonly Vector3[] DoorHome =
        {
            new Vector3( 0.161f, 0f, -4.76f),   // direita interna (faixa)
            new Vector3( 0.484f, 0f, -4.83f),   // direita externa
            new Vector3(-0.161f, 0f, -4.76f),   // esquerda interna
            new Vector3(-0.484f, 0f, -4.83f),   // esquerda externa
        };
        static readonly float[] DoorTravel = { 0.645f, 0.50f, -0.645f, -0.50f };
        static readonly string[] DoorModel = { "LauncherX_Porta1", "LauncherX_Porta2", "LauncherX_Porta1", "LauncherX_Porta2" };

        const float DoorSeconds = 1.5f;      // tempo para abrir/fechar
        const float ExitDistance = 7.7f;     // quanto o missil anda ate a cauda sair do topo
        const float ReappearDelay = 4.0f;    // depois do disparo, espera antes de subir o proximo
        const float RiseSeconds = 2.0f;
        const float RiseDepth = 4.0f;

        static readonly MyDefinitionId MagazineId = new MyDefinitionId(typeof(MyObjectBuilder_AmmoMagazine), "LauncherXMissile");

        IMySmallMissileLauncher _block;
        readonly MyEntity[] _doors = new MyEntity[4];
        MyEntity _display;
        bool _created;
        int _frames;
        float _doorPos;                       // 0 fechada, 1 aberta
        bool _hasAmmo;

        // estado do missil exibido
        IMyMissile _launching;                // missil saindo agora (escondido, o modelo do poco acompanha)
        float _sinceShot = -1f;               // -1 = nenhum disparo recente
        bool _displayShown;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _block = (IMySmallMissileLauncher)Entity;
            if (!MyAPIGateway.Utilities.IsDedicated)
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void Close()
        {
            for (int i = 0; i < _doors.Length; i++)
                if (_doors[i] != null) _doors[i].Close();
            if (_display != null) _display.Close();
            if (_launching != null) LauncherXSession.ShowMissile(_launching);
        }

        MyEntity Part(string model, Matrix local)
        {
            var e = new MyEntity();
            e.Init(null, LauncherXSession.ModPath + "\\Models\\LauncherX\\" + model + ".mwm", (MyEntity)_block, 1f);
            e.Save = false;
            e.SyncFlag = false;
            e.PositionComp.SetLocalMatrix(ref local);
            MyAPIGateway.Entities.AddEntity(e);
            return e;
        }

        bool Create()
        {
            if (_created) return true;
            if (++_frames < 30 || !_block.InScene || _block.CubeGrid.Physics == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var m = DoorMatrix(i, 0f);
                _doors[i] = Part(DoorModel[i], m);
            }
            _display = Part("LauncherX_Display", Matrix.CreateTranslation(MissileHome));
            _display.Render.Visible = false;
            _created = true;
            _doorPos = -1f;                   // forca posicionar as portas no primeiro update
            var inv = _block.GetInventory(0);
            _hasAmmo = inv != null && (float)inv.GetItemAmount(MagazineId) > 0f;
            return true;
        }

        Matrix DoorMatrix(int i, float open)
        {
            var m = i >= 2 ? Matrix.CreateRotationZ(MathHelper.Pi) : Matrix.Identity;
            m.Translation = DoorHome[i] + new Vector3(DoorTravel[i] * open, 0f, 0f);
            return m;
        }

        void SetDisplay(Vector3 local, bool visible)
        {
            var m = Matrix.CreateTranslation(local);
            _display.PositionComp.SetLocalMatrix(ref m);
            if (_displayShown != visible)
            {
                _display.Render.Visible = visible;
                _displayShown = visible;
            }
        }

        public void OnShot(IMyMissile missile)
        {
            if (_launching != null) LauncherXSession.ShowMissile(_launching);
            _launching = missile;
            _sinceShot = 0f;
            LauncherXSession.HideMissile(missile);
        }

        public void OnMissileGone(IMyMissile missile)
        {
            if (_launching == missile) _launching = null;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (!Create()) return;

                // ---- portas: abertas com o bloco funcionando e missil carregado (ou saindo)
                bool firing = _launching != null || (_sinceShot >= 0f && _sinceShot < ReappearDelay);
                float target = _block.IsWorking && (_hasAmmo || firing) ? 1f : 0f;
                if (_doorPos != target)
                {
                    float step = (1f / 60f) / DoorSeconds;
                    if (_doorPos < 0f) _doorPos = target;
                    else _doorPos = target > _doorPos ? Math.Min(target, _doorPos + step) : Math.Max(target, _doorPos - step);
                    float s = _doorPos * _doorPos * (3f - 2f * _doorPos);
                    for (int i = 0; i < 4; i++)
                    {
                        var m = DoorMatrix(i, s);
                        _doors[i].PositionComp.SetLocalMatrix(ref m);
                    }
                }

                // ---- municao (a cada meio segundo)
                if (_frames++ % 30 == 0)
                {
                    var inv = _block.GetInventory(0);
                    _hasAmmo = inv != null && (float)inv.GetItemAmount(MagazineId) > 0f;
                }

                // ---- missil saindo: o modelo do poco acompanha o missil real (escondido)
                if (_launching != null)
                {
                    var missile = _launching;
                    if (missile.MarkedForClose || missile.Closed)
                    {
                        _launching = null;
                    }
                    else
                    {
                        var inv = MatrixD.Invert(_block.WorldMatrix);
                        var local = (Vector3)Vector3D.Transform(missile.GetPosition(), inv);
                        float traveled = MissileHome.Z - local.Z;
                        if (traveled < ExitDistance)
                        {
                            SetDisplay(new Vector3(0f, 0f, Math.Min(local.Z, MissileHome.Z)), true);
                        }
                        else
                        {
                            // saiu do lancador: aletas abertas (modelo real) aparecem
                            LauncherXSession.ShowMissile(missile);
                            _launching = null;
                            SetDisplay(MissileHome, false);
                        }
                    }
                    if (_launching == null && _displayShown) SetDisplay(MissileHome, false);
                    if (_sinceShot >= 0f) _sinceShot += 1f / 60f;
                    return;
                }

                if (_sinceShot >= 0f)
                {
                    _sinceShot += 1f / 60f;
                    if (_sinceShot < ReappearDelay)
                    {
                        if (_displayShown) SetDisplay(MissileHome, false);
                        return;
                    }
                    if (!_hasAmmo)
                    {
                        _sinceShot = -1f;
                    }
                    else
                    {
                        float t = Math.Min(1f, (_sinceShot - ReappearDelay) / RiseSeconds);
                        t = t * t * (3f - 2f * t);
                        SetDisplay(MissileHome + new Vector3(0f, 0f, RiseDepth * (1f - t)), true);
                        if (t >= 1f) _sinceShot = -1f;
                        return;
                    }
                }

                if (_hasAmmo != _displayShown) SetDisplay(MissileHome, _hasAmmo);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("[LauncherX] " + e);
            }
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class LauncherXSession : MySessionComponentBase
    {
        public static string ModPath;
        const float FlameScale = 3f;          // fogo/fumaca proporcional ao missil de 1 bloco
        const float NozzleBack = 7.35f;       // distancia do nariz ate o bocal

        class Flight
        {
            public IMyMissile Missile;
            public VRage.Game.MyParticleEffect Effect;
            public LauncherXLogic Logic;
            public IMyCubeBlock Block;
            public bool Released;             // ja saiu da torre e foi endireitado
            public float Age;
        }

        const float ExitDistance = 7.7f;      // nariz anda isso ate a cauda passar do topo
        const float MissileHomeZ = -4.8f;
        const float ReleaseTimeout = 4f;      // seguranca: endireita mesmo que algo estranho aconteca

        static readonly List<Flight> _flights = new List<Flight>();

        // ---- explosao grande (visual): bola de fogo + segunda onda + fumaca que fica no ar
        class Boom
        {
            public Vector3D Pos;
            public float Time;
            public int Stage;
            public readonly List<VRage.Game.MyParticleEffect> Effects = new List<VRage.Game.MyParticleEffect>();
        }
        static readonly List<Boom> _booms = new List<Boom>();
        const float BoomLife = 12f;
        static readonly HashSet<long> _hidden = new HashSet<long>();

        public override void LoadData()
        {
            ModPath = ModContext.ModPath;
        }

        // Dependencia: o mod principal de misseis (missil-misterplace) da ao LauncherXMissile o comportamento
        // SLBM (sai debaixo d'agua e guia). Workshop: 3496380795 (final) e 3514275550 (dev); local: nome da pasta.
        static readonly ulong[] BrainWorkshopIds = { 3496380795UL, 3514275550UL };
        static readonly string[] BrainLocalNames = { "missil-misterplace", "missil-misterplace-dev" };

        static bool BrainLoaded()
        {
            var mods = MyAPIGateway.Session != null ? MyAPIGateway.Session.Mods : null;
            if (mods == null) return true;      // nao da para saber: nao incomoda
            foreach (var m in mods)
            {
                string name = m.Name ?? "";
                foreach (var local in BrainLocalNames)
                    if (name == local) return true;
                foreach (var id in BrainWorkshopIds)
                    if (m.PublishedFileId == id || name.StartsWith(id.ToString())) return true;
            }
            return false;
        }

        public override void BeforeStart()
        {
            if (!BrainLoaded())
            {
                MyLog.Default.WriteLineAndConsole("[LauncherX] Required mod missil-misterplace is not loaded: LauncherXMissile will not behave as SLBM ammo.");
                if (!MyAPIGateway.Utilities.IsDedicated)
                    MyAPIGateway.Utilities.ShowNotification("LauncherX: falta o mod principal de misseis (missil-misterplace). Sem ele o missil nao funciona como SLBM.", 15000, "Red");
            }
            if (MyAPIGateway.Missiles == null) return;
            MyAPIGateway.Missiles.OnMissileAdded += OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved += OnMissileRemoved;
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Missiles != null)
            {
                MyAPIGateway.Missiles.OnMissileAdded -= OnMissileAdded;
                MyAPIGateway.Missiles.OnMissileRemoved -= OnMissileRemoved;
            }
            foreach (var f in _flights) StopEffect(f);
            _flights.Clear();
            _booms.Clear();
            _hidden.Clear();
        }

        public static void HideMissile(IMyMissile missile)
        {
            var e = missile as MyEntity;
            if (e == null || e.Render == null) return;
            e.Render.Visible = false;
            _hidden.Add(missile.EntityId);
        }

        public static void ShowMissile(IMyMissile missile)
        {
            var e = missile as MyEntity;
            if (e == null || e.Render == null) return;
            if (_hidden.Remove(missile.EntityId)) e.Render.Visible = true;
        }

        static MatrixD NozzleMatrix(IMyMissile missile)
        {
            var m = missile.WorldMatrix;
            m.Translation = m.Translation - m.Forward * NozzleBack;
            return m;
        }

        void OnMissileAdded(IMyMissile missile)
        {
            try
            {
                var block = MyAPIGateway.Entities.GetEntityById(missile.LauncherId) as IMySmallMissileLauncher;
                if (block == null || block.BlockDefinition.SubtypeId != "LauncherX") return;
                var logic = block.GameLogic != null ? block.GameLogic.GetAs<LauncherXLogic>() : null;

                var f = new Flight { Missile = missile, Logic = logic, Block = block };
                if (!MyAPIGateway.Utilities.IsDedicated)
                {
                    var m = NozzleMatrix(missile);
                    VRage.Game.MyParticleEffect effect;
                    if (VRage.Game.MyParticlesManager.TryCreateParticleEffect("Smoke_Missile", m, out effect))
                    {
                        effect.UserScale = FlameScale;
                        f.Effect = effect;
                    }
                    if (logic != null) logic.OnShot(missile);
                }
                _flights.Add(f);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("[LauncherX] " + e);
            }
        }

        void OnMissileRemoved(IMyMissile missile)
        {
            try
            {
                bool ours = false;
                for (int i = 0; i < _flights.Count; i++) if (_flights[i].Missile == missile) ours = true;
                for (int i = _flights.Count - 1; i >= 0; i--)
                {
                    if (_flights[i].Missile != missile) continue;
                    StopEffect(_flights[i]);
                    if (_flights[i].Logic != null) _flights[i].Logic.OnMissileGone(missile);
                    _flights.RemoveAt(i);
                }
                ShowMissile(missile);   // o jogo reaproveita o objeto do missil
                if (ours && !MyAPIGateway.Utilities.IsDedicated) StartBoom(missile.GetPosition());
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("[LauncherX] " + e);
            }
        }

        static VRage.Game.MyParticleEffect Spawn(string name, Vector3D pos, float scale)
        {
            VRage.Game.MyParticleEffect e;
            var m = MatrixD.CreateTranslation(pos);
            var up = -MyAPIGateway.Physics.CalculateNaturalGravityAt(pos, out _dummyInterference);
            if (up.LengthSquared() > 0.01f)
                m = MatrixD.CreateWorld(pos, Vector3D.CalculatePerpendicularVector(up), Vector3D.Normalize(up));
            if (!VRage.Game.MyParticlesManager.TryCreateParticleEffect(name, m, out e)) return null;
            e.UserScale = scale;
            return e;
        }
        static float _dummyInterference;

        static void StartBoom(Vector3D pos)
        {
            var b = new Boom { Pos = pos };
            var e = Spawn("Explosion_Warhead_30", pos, 1.0f);       // bola de fogo principal
            if (e != null) b.Effects.Add(e);
            e = Spawn("Explosion_Debris", pos, 3.0f);               // destrocos voando
            if (e != null) b.Effects.Add(e);
            _booms.Add(b);
            PlaySound(pos);
        }

        static void PlaySound(Vector3D pos)
        {
            try
            {
                var emitter = new Sandbox.Game.Entities.MyEntity3DSoundEmitter(null);
                emitter.SetPosition(pos);
                emitter.PlaySound(new Sandbox.Game.Entities.MySoundPair("WepLrgWarheadExpl"));
            }
            catch (Exception) { }
        }

        static void UpdateBooms()
        {
            for (int i = _booms.Count - 1; i >= 0; i--)
            {
                var b = _booms[i];
                b.Time += 1f / 60f;
                if (b.Stage == 0 && b.Time > 0.25f)
                {
                    b.Stage = 1;                                        // segunda onda de fogo, menor
                    var e = Spawn("Explosion_Warhead_15", b.Pos, 1.2f);
                    if (e != null) b.Effects.Add(e);
                }
                if (b.Stage == 1 && b.Time > 0.8f)
                {
                    b.Stage = 2;                                        // fumaca escura subindo
                    var e = Spawn("Meteorit_Smoke1AfterHit", b.Pos, 4.0f);
                    if (e != null) b.Effects.Add(e);
                }
                if (b.Time > BoomLife)
                {
                    foreach (var e in b.Effects) if (e != null && !e.IsStopped) e.Stop(false);
                    _booms.RemoveAt(i);
                }
            }
        }

        // Enquanto esta dentro da torre o missil herda o movimento da nave (senao bate na parede).
        // Ao sair pelo topo, tira o deslocamento lateral/para tras herdado e segue reto na linha do disparo.
        static void Release(Flight f)
        {
            f.Age += 1f / 60f;
            bool outOfTower = f.Age > ReleaseTimeout;
            Vector3 shipVel = Vector3.Zero;
            if (f.Block != null && !f.Block.Closed && f.Block.CubeGrid != null && f.Block.CubeGrid.Physics != null)
            {
                var local = Vector3D.Transform(f.Missile.GetPosition(), MatrixD.Invert(f.Block.WorldMatrix));
                if (MissileHomeZ - local.Z >= ExitDistance) outOfTower = true;
                shipVel = f.Block.CubeGrid.Physics.GetVelocityAtPoint(f.Block.WorldMatrix.Translation);
            }
            else outOfTower = true;
            if (!outOfTower) return;

            var fwd = (Vector3)f.Missile.WorldMatrix.Forward;
            var v = f.Missile.LinearVelocity;
            float own = Vector3.Dot(v - shipVel, fwd);           // velocidade propria do missil
            float carry = Math.Max(0f, Vector3.Dot(shipVel, fwd)); // so mantem a nave se ela ia na mesma direcao
            f.Missile.LinearVelocity = fwd * Math.Max(3f, own + carry);
            f.Released = true;
        }

        static void StopEffect(Flight f)
        {
            if (f.Effect == null) return;
            f.Effect.Stop(false);
            f.Effect = null;
        }

        public override void UpdateAfterSimulation()
        {
            if (_booms.Count > 0)
            {
                try { UpdateBooms(); } catch (Exception e) { MyLog.Default.WriteLineAndConsole("[LauncherX] " + e); }
            }
            if (_flights.Count == 0) return;
            try
            {
                for (int i = _flights.Count - 1; i >= 0; i--)
                {
                    var f = _flights[i];
                    if (f.Missile.Closed || f.Missile.MarkedForClose)
                    {
                        StopEffect(f);
                        _flights.RemoveAt(i);
                        continue;
                    }
                    if (!f.Released) Release(f);
                    if (f.Effect != null) f.Effect.WorldMatrix = NozzleMatrix(f.Missile);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("[LauncherX] " + e);
            }
        }
    }
}
