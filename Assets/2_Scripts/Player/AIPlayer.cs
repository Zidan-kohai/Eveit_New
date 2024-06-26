using Cinemachine;
using DG.Tweening;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.GlobalIllumination;

public class AIPlayer : MonoBehaviour, IPlayer, ISee, IHumanoid, IMove
{
    [Header("Movement")]
    [SerializeField] private float startSpeedOnPlayerUp = 50f;
    [SerializeField] private float maxSpeedOnPlayerUp = 80f;
    [SerializeField] private float startSpeedOnPlayerFall = 20f;
    [SerializeField] private float maxSpeedOnPlayerFall = 30f;
    [SerializeField] private float currrentSpeed = 50f;
    [SerializeField] private int speedScaleFactor = 3;
    [SerializeField] private float currrentMinSpeed = 1f;
    [SerializeField] private float currrentMaxSpeed = 1f;
    [SerializeField] private List<Transform> pointsToWalk;
    [SerializeField] private int currnetWalkPointIndex;
    [SerializeField] private bool isTeleport;

    [Space, Header("Components")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private ReachArea reachArea;
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private CapsuleCollider playerCollider;
    [SerializeField] private Transform playerVisual;

    [Header("Humanoids")]
    [SerializeField] private List<IEnemy> enemies = new List<IEnemy>();
    [SerializeField] private List<IPlayer> players = new List<IPlayer>();

    [Header("State")]
    [SerializeField] private PlayerState state;
    [SerializeField] private float timeToUpFromFall;
    [SerializeField] private float timeToDeathFromFall;
    [SerializeField] private float lostedTimeFromFallToUp;
    [SerializeField] private float lostedTimeFromFallToDeath;

    [Header("Idle")]
    [SerializeField] private float lastedTimeFromStartIdleToRotate = 0f;
    [SerializeField] private float timeToChangeState = 5f;
    [SerializeField] private float currentTimeToChangeState = 0f;
    [SerializeField] private int inverseRotateOnIdle = 1;
    [SerializeField] private int rotatingSpeedOnIdle = 5;
    [SerializeField] private int chanseToChangeState = 5;

    [Header("Escape")]
    [SerializeField] private float minEscapeTime;
    [SerializeField] private float maxEscapeTime;
    [SerializeField] private Coroutine stopEscapeCoroutine;

    [Header("Help")]
    [SerializeField] private float carryDistance;
    [SerializeField] private float safeDistance;
    [SerializeField] private float helpDistance;
    [SerializeField] private int helpCount;
    [SerializeField] private IPlayer playerToHelp;
    [SerializeField] private IPlayer carriedPlayer;

    [Header("General")]
    [SerializeField] private int moneyMultiplierFactor = 1;
    [SerializeField] private int experienceMultiplierFactor = 1;
    [SerializeField] private Transform carriedTransform;
    [SerializeField] private float livedTime = 0;
    [SerializeField] private Light pointLight;
    private Action<IPlayer> playerFallEvent;
    private Coroutine coroutine;
    private string name;

    private void Update()
    {
        if (state == PlayerState.Death) return;

        livedTime += Time.deltaTime;

        switch (state)
        {
            case PlayerState.Idle:
                if(!CheckPlayerAndEnemyToHelp())
                    OnIdle();
                break;
            case PlayerState.Walk:
                if (!CheckPlayerAndEnemyToHelp())
                    OnWalk();
                break;
            case PlayerState.Escape:
                if(!CheckPlayerAndEnemyToHelp())
                    MoveAwayFromEnemies();
                break;
            case PlayerState.Carry:
                MoveAwayFromEnemies();
                break;
            case PlayerState.Carried:
                OnCarried();
                break;
            case PlayerState.Fall:
                OnFall();
                break;
            case PlayerState.Raising:
                break;
            case PlayerState.Death:
                break;
        }
    }

    private void OnCarried()
    {
        gameObject.transform.position = playerVisual.position;
    }

    public void Initialize(List<Transform> Points, Vector3 spawnPoint)
    {
        ChangeState(PlayerState.Idle);

        transform.position = spawnPoint;
        agent.speed = startSpeedOnPlayerUp;
        reachArea.SetISee(this);
        animationController.SetIMove(this);
        gameObject.SetActive(true);

        pointsToWalk = Points;

        CameraConrtoller.AddCameraST(this, virtualCamera);

        name = Helper.GetRandomName();

        GameplayController.AddPlayerST(this);
        BuffHandler.AddPlayerST(this);
    }

    public void Carried(Transform point, CinemachineVirtualCamera virtualCamera)
    {
        ChangeState(PlayerState.Carried);
        playerVisual.position = point.transform.position;
        playerVisual.parent = point.transform;
        playerVisual.localEulerAngles = Vector3.zero; 
        playerCollider.enabled = false;
    }

    public string GetName() => name;

    public void SetTimeToUp(int deacreaseFactor)
    {
        timeToUpFromFall /= deacreaseFactor; 
    }

    public int GetEarnedMoney()
    {
        int earnedMoney = (helpCount * 10) + 50;

        earnedMoney *= moneyMultiplierFactor;

        return earnedMoney;
    }

    public int GetEarnedExperrience()
    {
        int earnedExperience = (helpCount * 5) + 25;

        earnedExperience *= experienceMultiplierFactor;

        return earnedExperience;
    }

    public int GetHelpCount() => helpCount;

    public float GetSurvivedTime() => livedTime + 0.2f;

    public void GetDownOnGround()
    {
        transform.position = playerVisual.transform.position; 
        agent.isStopped = false;
        playerVisual.parent = transform;
        playerVisual.localPosition = new Vector3(0, 1, 0);
        playerVisual.localEulerAngles = Vector3.zero;
        animationController.PutPlayer();
        playerCollider.enabled = true;
        ChangeState(PlayerState.Fall);
    }

    public void SubscribeOnFall(Action<IPlayer> onPlayerDeath)
    {
        playerFallEvent += onPlayerDeath;
    }

    public void AddHumanoid(IHumanoid IHumanoid)
    {
        if (state == PlayerState.Death) return;

        if (IHumanoid.gameObject.TryGetComponent(out IEnemy enemy))
        {
            enemies.Add(enemy);
            if(state != PlayerState.Carry)
                ChangeState(PlayerState.Escape);
        }
        else if (IHumanoid.gameObject.TryGetComponent(out IPlayer player) && !IHumanoid.gameObject.TryGetComponent(out Bait bait))
        {
            players.Add(player);
        }
    }

    public void RemoveHumanoid(IHumanoid IHumanoid)
    {
        if (state == PlayerState.Death) return;

        if (IHumanoid.gameObject.TryGetComponent(out IEnemy enemy))
        {
            enemies.Remove(enemy);

            if(enemies.Count == 0)
            {
                float escapeTime = UnityEngine.Random.Range(minEscapeTime, maxEscapeTime);

                if(stopEscapeCoroutine != null)
                {
                    StopCoroutine(stopEscapeCoroutine);
                }

                stopEscapeCoroutine = StartCoroutine(Wait(escapeTime, () =>
                {
                    if (enemies.Count == 0 && !IsFallOrDeath() && state != PlayerState.Carried) ChangeState(PlayerState.Idle);
                }));
            }
        }
        else if (IHumanoid.gameObject.TryGetComponent(out IPlayer player))
        {
            players.Remove(player);
        }
    }

    public bool GetIsTeleport()
    {
        return isTeleport;
    }

    public void Teleport(Vector3 teleportPosition)
    {
        isTeleport = true;
        transform.position = teleportPosition;
        DOTween.Sequence()
            .AppendInterval(0.3f)
            .AppendCallback(() => isTeleport = false);
    }

    [ContextMenu("Fall")]
    public void Fall()
    {
        if (state == PlayerState.Death) return;

        if (state == PlayerState.Carry) PutPlayer();

        PointerManager.Instance.AddToList(this);

        ChangeState(PlayerState.Fall);
    }

    public Transform GetTransform()
    {
        return transform;
    }

    public bool IsFallOrDeath()
    {
        return state == PlayerState.Fall || state == PlayerState.Death || state == PlayerState.Carried;
    }

    public bool IsFall()
    {
        return state == PlayerState.Fall;
    }

    public bool IsDeath()
    {
        return state == PlayerState.Death;
    }

    public float Raising()
    {
        ChangeState(PlayerState.Raising);

        if (coroutine != null) StopCoroutine(coroutine);

        coroutine = StartCoroutine(Wait(0.5f, () =>
        {
            if (state != PlayerState.Carried)
                ChangeState(PlayerState.Fall);
        }));

        lostedTimeFromFallToUp -= Time.deltaTime;


        if(lostedTimeFromFallToUp <= 0)
        {
            ChangeState(PlayerState.Idle); 
            StopCoroutine(coroutine);
        }

        return Mathf.Abs(lostedTimeFromFallToUp / timeToUpFromFall - 1);
    }

    public float GetPercentOfRaising()
    {
        return Mathf.Abs(lostedTimeFromFallToUp / timeToUpFromFall - 1);
    }

    public float MoveSpeed()
    {
        return agent.velocity.magnitude;
    }

    public bool IsJump()
    {
        return false;
    }

    private void ChangeState(PlayerState newState)
    {
        //if state and newState are equal we don`t do anything
        //if current state is fall we can switch only to the idle, death or carried state
        //if current state is carried we can switch only to the fall
        //if current state is death we don`t do anything
        if (state == newState 
            ||

            ((state == PlayerState.Fall)
            && (newState != PlayerState.Idle)
            && (newState != PlayerState.Death))
            && (newState != PlayerState.Carried)

            ||
            (state == PlayerState.Carried)
            && newState != PlayerState.Fall
            
            || state == PlayerState.Death) return;

        state = newState;

        switch (state)
        {
            case PlayerState.Idle:
                animationController.Up();
                currrentMinSpeed = startSpeedOnPlayerUp;
                currrentMaxSpeed = maxSpeedOnPlayerUp;

                PointerManager.Instance.RemoveFromList(this);
                break;

            case PlayerState.Walk:
                break;

            case PlayerState.Fall:
                PlayerStateShower.ShowAIState(name, state);
                animationController.Fall();
                currrentMinSpeed = startSpeedOnPlayerFall;
                currrentMaxSpeed = maxSpeedOnPlayerFall;
                currrentSpeed = currrentMinSpeed;
                lostedTimeFromFallToUp = timeToUpFromFall;
                lostedTimeFromFallToDeath = timeToDeathFromFall;
                playerFallEvent?.Invoke(this);
                break;

            case PlayerState.Carry:
                animationController.Carry();
                break;

            case PlayerState.Carried:
                animationController.Carried();
                agent.isStopped = true;
                break;

            case PlayerState.Death:
                PointerManager.Instance.RemoveFromList(this);
                Death();
                break;
        }
    }

    private void Death()
    {
        if(coroutine != null) StopCoroutine(coroutine);

        PlayerStateShower.ShowAIState(name, state);
        agent.SetDestination(transform.position);
        animationController.Death();
    }

    private void OnIdle()
    {
        lastedTimeFromStartIdleToRotate += Time.deltaTime;
        currentTimeToChangeState += Time.deltaTime;

        if (lastedTimeFromStartIdleToRotate > 3f)
        {
            inverseRotateOnIdle *= -1;
            lastedTimeFromStartIdleToRotate = 0f;
        }

        transform.Rotate(0, rotatingSpeedOnIdle * Time.deltaTime * inverseRotateOnIdle, 0);

        bool endIdle = UnityEngine.Random.Range(0, 1000) < chanseToChangeState;

        if (endIdle || currentTimeToChangeState > timeToChangeState)
        {
            ChangeState(PlayerState.Walk);
            currentTimeToChangeState = 0f;
        }

        currrentSpeed -= Time.deltaTime;
        currrentSpeed = Mathf.Clamp(currrentSpeed, 0, currrentMaxSpeed);
    }

    private void OnWalk()
    {
        SetDestination(pointsToWalk[currnetWalkPointIndex].position);   

        if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending)
        {
            currnetWalkPointIndex = UnityEngine.Random.Range(0, pointsToWalk.Count);
        }
    }

    private void OnFall()
    {
        lostedTimeFromFallToDeath -= Time.deltaTime;

        if(lostedTimeFromFallToDeath < 0)
        {
            ChangeState(PlayerState.Death);
        }
        else if(players.Count > 0)
        {
            WalkToPlayerOnFail();
        }
        else
        {
            OnWalk();
        }
    }

    private void WalkToPlayerOnFail()
    {
        bool isHaveUpPlayer = false;
        float distanceToPlayer = float.PositiveInfinity;
        IPlayer nearnestPlayer = null;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].IsDeath()) continue;

            float distance = (players[i].GetTransform().position - transform.position).magnitude;
            bool isPlayerUp = !players[i].IsFall();

            if (isHaveUpPlayer == false && isPlayerUp == true)
            {
                isHaveUpPlayer = true;
                distanceToPlayer = distance;
                nearnestPlayer = players[i];
            }
            else if (isHaveUpPlayer == isPlayerUp)
            {
                if (distance < distanceToPlayer)
                {
                    distanceToPlayer = distance;
                    nearnestPlayer = players[i];
                }
            }
        }
        if(nearnestPlayer != null) 
            SetDestination(nearnestPlayer.GetTransform().position);
    }

    private void MoveAwayFromEnemies()
    {
        Vector3 escapeDirection = CalculateEscapeDirection();

        int distance = 25;
        Vector3 escapePoint = transform.position + escapeDirection * distance;

        if (CheckGround(escapePoint))
        {
            SetDestination(escapePoint);
        }
        else
        {
            TryAlternativeRoutes(escapeDirection);
        }
    }

    private Vector3 CalculateEscapeDirection()
    {
        Vector3 averageApproachDirection = Vector3.zero;

        foreach (var enemy in enemies)
        {
            Vector3 toEnemy = enemy.GetTransform().position - transform.position;
            averageApproachDirection += toEnemy.normalized * (1.0f / toEnemy.magnitude);
        }
        if (enemies.Count > 0)
        {
            averageApproachDirection /= enemies.Count;
            return -averageApproachDirection.normalized;
        }
        else if(enemies.Count == 0 && state == PlayerState.Carry)
        {
            PutPlayer();
            ChangeState(PlayerState.Idle);
        }

        return transform.forward;
    }

    private bool CheckGround(Vector3 point)
    {
        RaycastHit hit;
        if (Physics.Raycast(point + Vector3.up * 1.0f, Vector3.down, out hit, 2.0f))
        {
            return hit.collider != null; // Check if we hit the ground
        }
        return false;
    }

    private void TryAlternativeRoutes(Vector3 initialDirection)
    {
        Vector3[] alternatives = { transform.right, -transform.right };
        int distance = 25;

        foreach (Vector3 direction in alternatives)
        {
            Vector3 point = transform.position + direction * distance;
            if (CheckGround(point))
            {
                SetDestination(point);
                return;
            }
        }

        Debug.Log("No valid escape route found!");
        agent.SetDestination(pointsToWalk[UnityEngine.Random.Range(0, pointsToWalk.Count)].position);
    }

    private bool CheckPlayerAndEnemyToHelp()
    {
        if (//state != PlayerState.Idle && state != PlayerState.Walk || 
            players.Count == 0) return false;

        #region CheckPlayer

        float distanceNeanestToPlayer = float.PositiveInfinity;
        IPlayer nearnestPlayer = null;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].IsDeath()) continue;

            float distance = (players[i].GetTransform().position - transform.position).magnitude;
            bool isPlayerFail = players[i].IsFall();

            if(isPlayerFail && distance < distanceNeanestToPlayer)
            {
                distanceNeanestToPlayer = distance;
                nearnestPlayer = players[i];
            }
        }

        #endregion

        #region Check Enemy

        float distanceToNeanestEnemy = float.PositiveInfinity;
        IEnemy nearnestEnemy = null;

        if (nearnestPlayer == null) return false;

        CheckEnemyDistanceBetweenPlayerToHelpAndEnemy(ref distanceToNeanestEnemy, ref nearnestEnemy, nearnestPlayer);
        
        if(HasEnemyBetweenFallPlayerAndThisPlayerToHelp(ref nearnestPlayer, distanceNeanestToPlayer)) return false;
        

        Debug.Log("nearnestPlayer: " + distanceNeanestToPlayer);
        Debug.Log("distanceToNeanestEnemy: " + distanceToNeanestEnemy);
        #endregion

        if (distanceToNeanestEnemy < safeDistance) return false;

        playerToHelp = nearnestPlayer;

        TryHelp(nearnestPlayer, distanceToNeanestEnemy);

        return true;
    }

    private void TryHelp(IPlayer player, float distanceToNeanestEnemy)
    {
        float distanceToPlayer = (player.GetTransform().position - transform.position).magnitude;

        if(distanceToPlayer < helpDistance)
        {
            if(distanceToNeanestEnemy < carryDistance && state != PlayerState.Carry)
            {
                Carry(player);
            }
            else if(player.Raising() >= 1)
            {
                helpCount++;
            }
        }
        else
        {
            SetDestination(player.GetTransform().position);
        }
    }

    private void Carry(IPlayer player)
    {
        player.Carried(carriedTransform, virtualCamera);
        ChangeState(PlayerState.Carry);
        carriedPlayer = player;
    }

    private void PutPlayer()
    {
        animationController.PutPlayer();
        carriedPlayer.GetDownOnGround();
        carriedPlayer = null;
    }

    private void CheckEnemyDistanceBetweenPlayerToHelpAndEnemy(ref float distanceToNeanestEnemy, ref IEnemy nearnestEnemy, IPlayer nearnestPlayer)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            float distance = (enemies[i].GetTransform().position - nearnestPlayer.GetTransform().position).magnitude;

            if (distance < distanceToNeanestEnemy)
            {
                distanceToNeanestEnemy = distance;
                nearnestEnemy = enemies[i];
            }
        }
    }

    private bool HasEnemyBetweenFallPlayerAndThisPlayerToHelp(ref IPlayer nearnestPlayer, float distanceToNearnestPlayer)
    {
        bool result = false;

        Vector3 directionToTarget = nearnestPlayer.GetTransform().position - transform.position;

        foreach (IEnemy enemy in enemies)
        {
            float distnaceBetweenThisPlayerAndEnemy = (enemy.GetTransform().position - transform.position).magnitude;

            if(distnaceBetweenThisPlayerAndEnemy < distanceToNearnestPlayer)
            {
                Vector3 directionToEnemyFromThisPlayer = enemy.GetTransform().position - transform.position;

                float angleBetweenEnemyAndPlayerToHelp = Vector3.Angle(directionToEnemyFromThisPlayer, directionToTarget);
                Debug.Log("AngleBetweenEnemyAndPlayerToHelp: " + angleBetweenEnemyAndPlayerToHelp);

                if(angleBetweenEnemyAndPlayerToHelp < 45)
                {
                    result = true;
                }

            }

        }

        return result;
    }

    private void SetDestination(Vector3 target)
    {
        currrentSpeed += Time.deltaTime * speedScaleFactor;

        currrentSpeed = Mathf.Clamp(currrentSpeed, currrentMinSpeed, currrentMaxSpeed);

        agent.speed = currrentSpeed;

        agent.SetDestination(target);
    }

    private IEnumerator Wait(float time, Action action)
    {
        yield return new WaitForSeconds(time);

        action.Invoke();
    }

    public void DisableLight()
    {
        StartCoroutine(Wait(0.5f, () => pointLight.enabled = false));
    }

    public void EnabledLight()
    {
        pointLight.enabled = true;
    }
}
