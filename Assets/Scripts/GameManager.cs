﻿using UnityEngine;
using System;
using System.Collections.Generic;

public partial class GameManager: MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameManager gm = Resources.Load("Prefabs/GameManager", typeof(GameManager)) as GameManager;
                gm = Instantiate(gm);
                _instance = gm;
            }
            return _instance;
        }
    }

    public EventHandler<ScoreArgs> OnCurrentScoreChange;
    public EventHandler<LevelMsgArgs> OnLevelMessage;
    public EventHandler<ScoreArgs> OnPlayerLifeChange;
    public EventHandler<LevelMsgArgs> OnFloatingMessage;

    private int _currentScore;
    private int _playerLife;

    public PacManController PacManPref;
    public Persona InkyPref;
    public Persona PinkyPref;
    public Persona BlinkyPref;
    public Persona ClydePref;

    private List<Persona> _personas = new List<Persona>();
    private List<Vector3> _playerSpawn = new List<Vector3>();
    private List<Vector3> _ghostSpawn = new List<Vector3>();
    private List<Vector3> _homeEnter = new List<Vector3>();

    public Vector3 GetHomeEnter(Vector3 position)
    {
        float dist = float.MaxValue; int id = 0;
        for (int i = 0; i < _homeEnter.Count; i++)
        {
            float newDist = Vector3.Distance(position, _homeEnter[i]);
            if (dist > newDist) { dist = newDist; id = i; }
        }
        return _homeEnter[id];
    }
    public Vector3 GetGhostSpawn
    {
        get { return _ghostSpawn[UnityEngine.Random.Range(0, _ghostSpawn.Count)]; }
    }

    public EventHandler<LevelWaveArgs> GhostBehaviourEvent;
    public float[] LevelWaveTimes;
    public float LevelFrightTime;
    public float _activeWaveTimer;
    public int _activeWave;
    private bool _levelStarted = false;

    public float _frightTime;
    public float FrightTime
    {
        get { return _frightTime; }
    }
    private int _combo;

    void Awake()
    {
        if (_instance == null) { _instance = this; }
        else { Destroy(this.gameObject); }
    }

	void Start ()
    {
        PrepareLevel();
    }

    void Update()
    {
        if (_levelStarted)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                PacManController pcm = _personas[0].GetComponent<PacManController>();
                HandlePlayerDead(pcm);
            }
            UpdateTimers();
            foreach(Persona p in _personas) { p.OnUpdate(); }
        }
    }
    
    private void PrepareLevel()
    {
        CreateMap();
        ResetTimers();
        CreateClassicGamePersona();
        _playerLife = 2;

        OnTriggerPlayerLifeChange();
        OnTriggerLevelMessageChange(new Vector3(14, 15.5f, 0), "READY!");

        Invoke("StartLevel", 3);
    }
    private void RestartLevel()
    {
        ResetTimers();
        CreateClassicGamePersona();
        OnTriggerLevelMessageChange(new Vector3(14, 15.5f, 0), "READY!");

        Invoke("StartLevel", 3);
    }
    private void ResetTimers()
    {
        _frightTime = 0;
        _activeWave = -1;

        _activeWaveTimer = 0;
    }

    private void StartLevel()
    {
        _levelStarted = true;
        OnTriggerLevelMessageChange(new Vector3(14, 15.5f, 0), "");
    }
    private void UpdateTimers()
    {
        if (_frightTime > 0)
        {
            _frightTime -= Time.deltaTime;
        }
        else
        {
            _combo = 0;
            if (_activeWaveTimer > 0)
            {
                _activeWaveTimer -= Time.deltaTime;
            }
            else
            {
                _activeWave++;
                if (_activeWave >= LevelWaveTimes.Length) { _activeWaveTimer = float.MaxValue; }
                else { _activeWaveTimer = LevelWaveTimes[_activeWave]; }
                if (_activeWave%2 == 0)
                {
                    if (GhostBehaviourEvent != null) { GhostBehaviourEvent(this, new LevelWaveArgs(GhostState.Scatter)); }
                }
                else
                {
                    if (GhostBehaviourEvent != null) { GhostBehaviourEvent(this, new LevelWaveArgs(GhostState.Chase)); }
                }
            }
        }
    }
}

public partial class GameManager
{
    private Map2DCyclic<PMNode> _map;
    public Map2DCyclic<PMNode> Map
    {
        get { return _map; }
    }

    public PacManMapTXTExtractor MapCreator;
    public PacManMapVisualizer MapVisualizer;

    private void CreateMap()
    {
        _map = MapCreator.GetMap();
        if (MapVisualizer != null)
        {
            MapVisualizer.CreateOrRefreshVisualData();

            for (int x = 0; x < Map.SizeX; x++)
            {
                for (int y = 0; y < Map.SizeY; y++)
                {
                    if (Map[x,y].CType == CellType.PacManSpawn)
                    {
                        _playerSpawn.Add(new Vector3(x + 0.5f, y + 0.5f, 0));
                    }
                    else if (Map[x,y].CType == CellType.GhostHome)
                    {
                        _ghostSpawn.Add(new Vector3(x + 0.5f, y + 0.5f, 0));
                    }
                    else if (Map[x,y].CType == CellType.HomeEnter)
                    {
                        _homeEnter.Add(new Vector3(x + 0.5f, y + 0.5f, 0));
                    }
                }
            }
        }
        else { Debug.Log("No map visualizator"); }
    }

    private void CreateClassicGamePersona()
    {
        if (_personas != null)
        {
            // clear personas
            if (_personas.Count > 0)
            {
                for (int i = 0; i < _personas.Count; i++)
                {
                    GhostBehaviour ghost = _personas[i].GetComponent<GhostBehaviour>();
                    if (ghost != null) { GhostBehaviourEvent -= ghost.GhostBehaviourEventHandler; }
                    Destroy(_personas[i].gameObject);
                }
                _personas.Clear();
            }

            //Create new personas
            PacManController player = Instantiate(PacManPref, _playerSpawn[UnityEngine.Random.Range(0, _playerSpawn.Count)], Quaternion.identity) as PacManController;
            player.CollisionEvent += OnPlayerCollisionHandler;
            _personas.Add(player);

            Vector3 spawnpoint = _ghostSpawn[UnityEngine.Random.Range(0, _ghostSpawn.Count)];
            GhostBehaviour blinky = Instantiate(InkyPref, spawnpoint, Quaternion.identity) as GhostBehaviour;
            GhostBehaviourEvent += blinky.GhostBehaviourEventHandler;
            blinky.SetData(player.transform, spawnpoint, new Vector3(1, 1, 0));
            blinky.name = "Blinky";
            _personas.Add(blinky);

            spawnpoint = _ghostSpawn[UnityEngine.Random.Range(0, _ghostSpawn.Count)];
            blinky = Instantiate(InkyPref, spawnpoint, Quaternion.identity) as GhostBehaviour;
            GhostBehaviourEvent += blinky.GhostBehaviourEventHandler;
            blinky.SetData(player.transform, spawnpoint, new Vector3(1, 1, 0));
            blinky.name = "Blinky";
            _personas.Add(blinky);

            spawnpoint = _ghostSpawn[UnityEngine.Random.Range(0, _ghostSpawn.Count)];
            blinky = Instantiate(InkyPref, spawnpoint, Quaternion.identity) as GhostBehaviour;
            GhostBehaviourEvent += blinky.GhostBehaviourEventHandler;
            blinky.SetData(player.transform, spawnpoint, new Vector3(1, 1, 0));
            blinky.name = "Blinky";
            _personas.Add(blinky);
            //Instantiate(PinkyPref, _ghostSpawn[Random.Range(0, _ghostSpawn.Count)], Quaternion.identity);
            //Instantiate(ClydePref, _ghostSpawn[Random.Range(0, _ghostSpawn.Count)], Quaternion.identity);
            //Instantiate(BlinkyPref, _ghostSpawn[Random.Range(0, _ghostSpawn.Count)], Quaternion.identity);
        }
    }

    #region EventHandlers
    private void OnPlayerCollisionHandler(object sender, Collision2DArgs arg)
    {
        FoodPoint fp = arg.CollisionObject.GetComponent<FoodPoint>();
        if (fp != null) { HandleEatFoodEvent(fp); return; }

        GhostBehaviour gb = arg.CollisionObject.GetComponent<GhostBehaviour>();
        if (gb != null)
        {
            if (gb.IsFrattering) { HandleEatGhostEvent(gb); return; }
            else if (!gb.IsDead)
            {
                PacManController pmc = arg.Sender.GetComponent<PacManController>();
                HandlePlayerDead(pmc);
            }
        }
    }
    private void HandleEatFoodEvent(FoodPoint fp)
    {
        _currentScore += fp.Point; OnTriggerCurrentScoreChange();

        Vector3 position = fp.transform.position;
        int ix = Mathf.FloorToInt(position.x);
        int iy = Mathf.FloorToInt(position.y);

        if (_map[ix, iy].CType == CellType.Energizer)
        {
            _frightTime = LevelFrightTime;
            if (GhostBehaviourEvent != null) GhostBehaviourEvent(this, new LevelWaveArgs(GhostState.Frightened));
        }
        _map[ix, iy].CType = CellType.None;
        MapVisualizer.RefreshVisualData(position);       
    }
    private void HandleEatGhostEvent(GhostBehaviour gb)
    {
        gb.OnDie();
        _combo++;
        int ghostPoint = GhostBehaviour.GhostPoint * _combo;
        _currentScore += ghostPoint;

        OnTriggerCurrentScoreChange();
        OnTriggerFloatingMessage(gb.transform.position, ghostPoint.ToString());
    }
    private void HandlePlayerDead(PacManController pmc)
    {
        if (pmc != null)
        {
            pmc.OnDie();
            _levelStarted = false;
            _playerLife--;

            if (_playerLife < 0) { Invoke("GameOver", 3); }
            else
            {
                OnTriggerPlayerLifeChange();
                Invoke("RestartLevel", 3);
            }
        }
    }

    private void OnTriggerCurrentScoreChange()
    {
        if (OnCurrentScoreChange != null) { OnCurrentScoreChange(this, new ScoreArgs(_currentScore)); }
    }
    private void OnTriggerLevelMessageChange(Vector3 position, string msg)
    {
        if (OnLevelMessage != null) { OnLevelMessage(this, new LevelMsgArgs(position, msg)); }
    }
    private void OnTriggerPlayerLifeChange()
    {
        if (OnPlayerLifeChange != null) { OnPlayerLifeChange(this, new ScoreArgs(_playerLife)); }
    }
    private void OnTriggerFloatingMessage(Vector3 position, string msg)
    {
        if (OnFloatingMessage != null) { OnFloatingMessage(this, new LevelMsgArgs(position, msg)); }
    }
    #endregion Eventhandlers
}
