using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class HeroKnight : MonoBehaviour {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_jumpForce = 7.5f;
    [SerializeField] GameObject m_slideDust;

    [Header("Sensors (Assign in Inspector)")]
    [SerializeField] private float sensorRadius = 0.1f;

    [Header("Dash & I-Frames")]
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    private bool canDash = true;
    private int originalLayer;

    [Header("Blocking")]
    public bool isBlocking = false;
    [SerializeField] private float blockDuration = 1.0f;
    [SerializeField] private float blockCooldown = 2.0f;
    private float blockTimer = 0f;
    private float blockDurationTimer = 0f;

    [Header("Jump Tuning")]
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Health & UI")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    [SerializeField] private HealthBar healthBar;

    [Header("Combat Hitbox")]
    [SerializeField] private GameObject attackHitbox; 

    private float coyoteTimeCounter;
    private Transform groundSensor, wallSensorR1, wallSensorR2, wallSensorL1, wallSensorL2;
    private LayerMask groundLayer;
    
    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private bool                m_isWallSliding = false;
    private bool                m_grounded = false;
    private bool                m_rolling = false;
    private int                 m_facingDirection = 1;
    private float               m_timeSinceAttack = 0.0f;
    private static HeroKnight instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        //DontDestroyOnLoad(gameObject);  
        //SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Force player to the spawn point instantly on scene load
        GameObject spawn = GameObject.FindGameObjectWithTag("Respawn");
        if (spawn != null) 
        {
            Debug.Log("spawning at spwn point");
            transform.position = spawn.transform.position;
        }
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        originalLayer = gameObject.layer;

        groundSensor = transform.Find("GroundSensor");
        wallSensorR1 = transform.Find("WallSensor_R1");
        wallSensorR2 = transform.Find("WallSensor_R2");
        wallSensorL1 = transform.Find("WallSensor_L1");
        wallSensorL2 = transform.Find("WallSensor_L2");

        groundLayer = LayerMask.GetMask("Ground");

        currentHealth = maxHealth;
        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);
    }

    void Update()
    {
        m_timeSinceAttack += Time.deltaTime;
        
        if (blockTimer > 0) blockTimer -= Time.deltaTime;
        
        if (isBlocking)
        {
            blockDurationTimer -= Time.deltaTime;
            if (blockDurationTimer <= 0) EndBlock();
        }

        UpdateGroundState();

        float inputX = Input.GetAxis("Horizontal");
        HandleMovement(inputX);
        UpdateAnimator(inputX);

        if (Input.GetMouseButtonDown(0) && m_timeSinceAttack > 0.25f && !m_rolling) 
        {
            if (isBlocking) EndBlock(); 
            PerformAttack();
        }
        else if (Input.GetMouseButtonDown(1) && !m_rolling && blockTimer <= 0 && !isBlocking) StartBlock();
        else if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !m_isWallSliding) PerformRoll();
        else if (Input.GetKeyDown(KeyCode.Space) && coyoteTimeCounter > 0f && !m_rolling) PerformJump();
    }

    private void UpdateGroundState()
    {
        m_grounded = IsGrounded();
        m_animator.SetBool("Grounded", m_grounded);

        if (m_grounded) coyoteTimeCounter = coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;
    }

    private void HandleMovement(float inputX)
    {
        if (inputX > 0) 
        { 
            GetComponent<SpriteRenderer>().flipX = false; 
            m_facingDirection = 1; 
            attackHitbox.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }
        else if (inputX < 0) 
        { 
            GetComponent<SpriteRenderer>().flipX = true; 
            m_facingDirection = -1; 
            attackHitbox.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }

        if (!m_rolling) m_body2d.linearVelocity = new Vector2(inputX * m_speed, m_body2d.linearVelocity.y);
    }

    private void UpdateAnimator(float inputX)
    {
        m_isWallSliding = ((IsWallRight() && inputX > 0) || (IsWallLeft() && inputX < 0)) && !m_grounded;
        m_animator.SetBool("WallSlide", m_isWallSliding);
        m_animator.SetFloat("speedY", m_body2d.linearVelocity.y); 

        bool isMoving = Mathf.Abs(inputX) > 0.1f;
        m_animator.SetBool("isMoving", isMoving && !m_isWallSliding);
    }

    private void PerformAttack()
    {
        m_timeSinceAttack = 0.0f; 
        m_animator.SetTrigger("Attack");
    }

    private void StartBlock()
    {
        isBlocking = true;
        blockDurationTimer = blockDuration;
        m_animator.SetTrigger("Block");
        m_animator.SetBool("IdleBlock", true);
    }

    private void EndBlock()
    {
        if (isBlocking)
        {
            isBlocking = false;
            m_animator.SetBool("IdleBlock", false);
            blockTimer = blockCooldown;
        }
    }

    private void PerformRoll()
    {
        if (isBlocking) EndBlock();
        StartCoroutine(PerformDash());
    }

    private void PerformJump()
    {
        m_animator.SetTrigger("Jump");
        m_grounded = false;
        m_animator.SetBool("Grounded", false);
        m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, m_jumpForce);
        coyoteTimeCounter = 0f;
    }

    private void TriggerDeath() => m_animator.SetTrigger("Death");
    private void TriggerHurt() => m_animator.SetTrigger("Hurt");

    private IEnumerator PerformDash()
    {
        canDash = false;
        m_rolling = true; 
        m_animator.SetTrigger("Roll");

        gameObject.layer = LayerMask.NameToLayer("Invincible");
        m_body2d.linearVelocity = new Vector2(m_facingDirection * dashForce, 0f);

        yield return new WaitForSeconds(dashDuration);

        gameObject.layer = originalLayer;
        m_rolling = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public void TakeDamage(float damageAmount, Transform attacker)
    {
        if(m_rolling) return;
        if (isBlocking && attacker != null)
        {
            float attackDirection = attacker.position.x - transform.position.x;
            if ((attackDirection > 0 && m_facingDirection == 1) || 
                (attackDirection < 0 && m_facingDirection == -1))
            {
                SuccessfulBlock();
                return; 
            }
        }

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); 

        if (healthBar != null) healthBar.UpdateHealth(currentHealth, maxHealth);

        if (currentHealth <= 0) Die();
        else TriggerHurt();
    }

    private async void Die()
    {
        TriggerDeath();
        await Task.Delay(800);
        Destroy(gameObject);
        GameManager.Instance.PlayerDied();
    }

    private void SuccessfulBlock()
    {
        m_animator.SetTrigger("Block");
        StartCoroutine(BlockInvincibility());
        EndBlock(); 
    }

    private IEnumerator BlockInvincibility()
    {
        gameObject.layer = LayerMask.NameToLayer("Invincible");
        yield return new WaitForSeconds(0.5f);
        if (!m_rolling) gameObject.layer = originalLayer;
    }

    private bool IsGrounded() => Physics2D.OverlapCircle(groundSensor.position, sensorRadius, groundLayer);
    private bool IsWallRight() => Physics2D.OverlapCircle(wallSensorR1.position, sensorRadius, groundLayer) && Physics2D.OverlapCircle(wallSensorR2.position, sensorRadius, groundLayer);
    private bool IsWallLeft() => Physics2D.OverlapCircle(wallSensorL1.position, sensorRadius, groundLayer) && Physics2D.OverlapCircle(wallSensorL2.position, sensorRadius, groundLayer);
    public void EnableHitbox() => attackHitbox.SetActive(true);
    public void DisableHitbox() => attackHitbox.SetActive(false);
        // Returns 0 when completely on cooldown, and 1 when fully ready
    public float GetShieldFillAmount()
    {
        if (isBlocking)
        {
            // Depletes from 1 to 0 while the shield is actively raised
            return blockDurationTimer / blockDuration; 
        }
        else
        {
            // Fills from 0 to 1 while on cooldown
            return 1f - (blockTimer / blockCooldown);
        }
    }
}