using System.Collections;
using UnityEngine;

public class GlacialGolem : MonoBehaviour
{
    public enum BossState { Chasing, Attacking, Hurt, Dead }
    public BossState currentState = BossState.Chasing;

    [Header("Stats")]
    [SerializeField] private float maxHealth = 600f;
    [SerializeField] private float moveSpeed = 2.5f;
    private float currentHealth;
    private bool isPhaseTwo = false;

    [Header("UI")]
    [SerializeField] private HealthBar bossHealthBar;

    [Header("Melee Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Ice Slam (AOE)")]
    [SerializeField] private float slamRange = 3f;
    [SerializeField] private float slamDamage = 35f;
    [SerializeField] private float slamCooldown = 5f;
    [SerializeField] private float slamWindupTime = 2f; // The 2-second telegraph
    [SerializeField] private GameObject slamVFXPrefab;
    [SerializeField] private GameObject warningCirclePrefab; // Slot your new red circle here
    private float slamTimer = 0f;

    [Header("Ice Spear (Projectile)")]
    [SerializeField] private float projectileRange = 8f;
    [SerializeField] private float projectileCooldown = 4f;
    [SerializeField] private GameObject iceSpearPrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private float projectileSpeed = 7f;
    private float projectileTimer = 0f;

    [Header("Frost Charge (Phase 2)")]
    [SerializeField] private float chargeSpeed = 9f;
    [SerializeField] private float chargeDamage = 40f;
    [SerializeField] private float chargeRange = 7f;
    [SerializeField] private float chargeCooldown = 7f;


    private float chargeTimer = 0f;

    private Transform player;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;

        if (bossHealthBar != null) bossHealthBar.UpdateHealth(currentHealth, maxHealth);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (currentState == BossState.Dead || player == null) return;

        slamTimer += Time.deltaTime;
        projectileTimer += Time.deltaTime;
        chargeTimer += Time.deltaTime;

        switch (currentState)
        {
            case BossState.Chasing:
                HandleChasing();
                break;
            case BossState.Attacking:
                break;
        }
    }

    // ─────────────────────────────────────────────
    //  CHASING + ATTACK SELECTION
    // ─────────────────────────────────────────────
    private void HandleChasing()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        animator.SetBool("isWalking", true);
        spriteRenderer.flipX = player.position.x > transform.position.x;
        transform.position = Vector2.MoveTowards(
            transform.position,
            new Vector2(player.position.x, transform.position.y),
            moveSpeed * Time.deltaTime);

        if (isPhaseTwo && dist >= chargeRange && chargeTimer >= chargeCooldown)
            StartCoroutine(ChargeAttack());
        else if (dist <= slamRange && slamTimer >= slamCooldown)
            StartCoroutine(IceSlamAttack());
        else if (dist <= projectileRange && dist > attackRange && projectileTimer >= projectileCooldown)
            StartCoroutine(IceSpearAttack());
        else if (dist <= attackRange)
            StartCoroutine(BasicMeleeAttack());
    }

    // ─────────────────────────────────────────────
    //  ATTACK 1 — BASIC MELEE
    //  Sprite: quick forward lunge then snap back
    // ─────────────────────────────────────────────
    private IEnumerator BasicMeleeAttack()
    {
        currentState = BossState.Attacking;
        animator.SetBool("isWalking", false);
        animator.SetTrigger("attack");

        Debug.Log("performing melee");


        // Lunge toward player
        Vector3 startPos = transform.position;
        Vector3 lungePos = transform.position + (Vector3)(((Vector2)player.position - (Vector2)transform.position).normalized * 0.6f);
        yield return StartCoroutine(MoveToPosition(startPos, lungePos, 0.15f));

        yield return new WaitForSeconds(0.4f);
        if (Vector2.Distance(transform.position, player.position) <= attackRange + 0.6f)
            player.GetComponent<HeroKnight>().TakeDamage(attackDamage, this.transform);

        // Snap back
        yield return StartCoroutine(MoveToPosition(transform.position, startPos, 0.2f));

        yield return new WaitForSeconds(attackCooldown);
        if (currentState != BossState.Dead) currentState = BossState.Chasing;
    }

    // ─────────────────────────────────────────────
    //  ATTACK 2 — ICE SLAM
    //  Sprite: stretch tall (wind-up), squash flat (slam)
    // ─────────────────────────────────────────────
    private IEnumerator IceSlamAttack()
    {
        currentState = BossState.Attacking;
        slamTimer = 0f;
        animator.SetBool("isWalking", false);
        Debug.Log("Slam windup started...");

        // 1. Spawn the warning circle at the boss's feet
        GameObject warning = null;
        if (warningCirclePrefab != null)
        {
            warning = Instantiate(warningCirclePrefab, projectileSpawnPoint.position, Quaternion.identity);
            warning.transform.localScale = Vector3.zero; // Start at size 0
        }

        // 2. The 2-Second Windup (Expand Circle & Flicker Boss)
        float elapsed = 0f;
        while (elapsed < slamWindupTime)
        {
            elapsed += Time.deltaTime;

            // Smoothly grow the circle to match the exact damage radius
            // (Multiplying by 2 because a default circle has a radius of 0.5)
            if (warning != null)
            {
                float currentScale = Mathf.Lerp(0f, slamRange * 2f, elapsed / slamWindupTime);
                warning.transform.localScale = new Vector3(currentScale, currentScale, 1f);
            }

            // Flicker the boss red and white rapidly
            if (Mathf.FloorToInt(elapsed * 15f) % 2 == 0)
                spriteRenderer.color = Color.red;
            else
                spriteRenderer.color = Color.white;

            yield return null; // Wait for next frame
        }

        // 3. Clean up windup visuals
        spriteRenderer.color = Color.white;
        if (warning != null) Destroy(warning);

        // 4. Play the explosion animation and VFX
        animator.SetTrigger("slam");
        
        if (slamVFXPrefab != null)
        {
            GameObject vfx = Instantiate(slamVFXPrefab, projectileSpawnPoint.position, Quaternion.identity);
            // Scales the explosion to exactly match your damage radius
            vfx.transform.localScale = new Vector3(slamRange, slamRange, 1f); 
        }

        // 5. Apply Damage (Only if player is still inside the slamRange)
        if (Vector2.Distance(projectileSpawnPoint.position, player.position) <= slamRange)
        {
            player.GetComponent<HeroKnight>().TakeDamage(slamDamage, this.transform);
        }

        // 6. Recovery phase before returning to chase
        yield return new WaitForSeconds(1f);
        if (currentState != BossState.Dead) currentState = BossState.Chasing;
    }

    // ─────────────────────────────────────────────
    //  ATTACK 3 — ICE SPEAR
    //  Sprite: double cyan flash as telegraph, then fires
    // ─────────────────────────────────────────────
    private IEnumerator IceSpearAttack()
    {
        currentState = BossState.Attacking;
        projectileTimer = 0f;
        animator.SetBool("isWalking", false);
        animator.SetTrigger("attack"); // Reuse attack anim
        Debug.Log("performing spear");

        // Telegraph: pulse cyan twice so the player knows a projectile is coming
        yield return StartCoroutine(FlashColor(Color.cyan, 0.15f));
        yield return StartCoroutine(FlashColor(Color.white, 0.1f));
        yield return StartCoroutine(FlashColor(Color.cyan, 0.15f));

        // Fire spear
        if (iceSpearPrefab != null && projectileSpawnPoint != null)
        {
            Vector2 dir = ((Vector2)player.position - (Vector2)projectileSpawnPoint.position).normalized;
            GameObject spear = Instantiate(iceSpearPrefab, projectileSpawnPoint.position, Quaternion.identity);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            spear.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f); // or + 90f depending on your sprite

            Rigidbody2D rb = spear.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = dir * projectileSpeed;

            IceSpear spearScript = spear.GetComponent<IceSpear>();
            if (spearScript != null) spearScript.damage = attackDamage * 1.2f;
        }

        yield return new WaitForSeconds(0.8f);
        if (currentState != BossState.Dead) currentState = BossState.Chasing;
    }

    // ─────────────────────────────────────────────
    //  ATTACK 4 — FROST CHARGE (Phase 2 only)
    //  Sprite: red flashes as warning, then dashes across screen
    // ─────────────────────────────────────────────
    private IEnumerator ChargeAttack()
    {
        currentState = BossState.Attacking;
        chargeTimer = 0f;
        animator.SetBool("isWalking", false);

        // Telegraph: rapid red flashes so the player can react
        yield return StartCoroutine(FlashColor(Color.red, 0.15f));
        yield return StartCoroutine(FlashColor(Color.white, 0.08f));
        yield return StartCoroutine(FlashColor(Color.red, 0.15f));
        yield return StartCoroutine(FlashColor(Color.white, 0.08f));
        yield return StartCoroutine(FlashColor(Color.red, 0.15f));
        yield return new WaitForSeconds(0.1f);

        // Lock in direction at the moment the charge starts
        //Vector2 chargeDir = ((Vector2)player.position - (Vector2)transform.position).normalized;

        Vector2 chargeDir;

        if(player.position.x < transform.position.x) chargeDir = new Vector2(-1, 0);
        else chargeDir = new Vector2(1, 0);
        spriteRenderer.flipX = chargeDir.x > 0;
        //animator.SetBool("isWalking", true);

        float elapsed = 0f;
        float chargeTime = 1.55f;
        bool hitPlayer = false;

        Debug.Log("performing charge" + chargeDir);


        while (elapsed < chargeTime)
        {
            transform.position += (Vector3)(chargeDir * chargeSpeed * Time.deltaTime);

            if (!hitPlayer && Vector2.Distance(transform.position, player.position) <= attackRange)
            {
                player.GetComponent<HeroKnight>().TakeDamage(chargeDamage, this.transform);
                hitPlayer = true;
                //StartCoroutine(ScaleTo(new Vector3(1.4f, 0.7f, 1f), 0.05f));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        //animator.SetBool("isWalking", false);
        spriteRenderer.color = Color.white;
        //yield return StartCoroutine(ScaleTo(originalScale, 0.15f));

        yield return new WaitForSeconds(0.4f);
        if (currentState != BossState.Dead) currentState = BossState.Chasing;
    }

    // ─────────────────────────────────────────────
    //  DAMAGE + PHASE 2 + DEATH
    // ─────────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        if (currentState == BossState.Dead) return;

        currentHealth -= damage;
        if (bossHealthBar != null) bossHealthBar.UpdateHealth(currentHealth, maxHealth);

        if (!isPhaseTwo && currentHealth <= maxHealth * 0.5f)
        {
            isPhaseTwo = true;
            moveSpeed *= 1.3f;
            attackCooldown *= 0.75f;
            StartCoroutine(Phase2Flash());
            Debug.Log("Glacial Golem — Phase 2!");
        }

        if (currentHealth <= 0)
            Die();
        else if (currentState != BossState.Attacking)
            StartCoroutine(HurtRoutine());
    }

    private IEnumerator HurtRoutine()
    {
        currentState = BossState.Hurt;
        animator.SetTrigger("hurt");
        yield return StartCoroutine(FlashColor(new Color(1f, 0.4f, 0.4f), 0.12f));
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.2f);
        if (currentState != BossState.Dead) currentState = BossState.Chasing;
    }

    private void Die()
    {
        currentState = BossState.Dead;
        StopAllCoroutines();
        transform.localScale = originalScale;
        spriteRenderer.color = Color.white;
        animator.SetTrigger("death");

        if (bossHealthBar != null) bossHealthBar.gameObject.SetActive(false);

        GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject, 3f);
    }

    // ─────────────────────────────────────────────
    //  SPRITE HELPERS
    // ─────────────────────────────────────────────

    // Smoothly move from a to b over duration seconds
    private IEnumerator MoveToPosition(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            transform.position = Vector3.Lerp(from, to, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = to;
    }

    // Smoothly scale to target over duration seconds
    private IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < duration)
        {
            transform.localScale = Vector3.Lerp(start, target, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = target;
    }

    // Flash sprite to a color then back to white
    private IEnumerator FlashColor(Color color, float duration)
    {
        spriteRenderer.color = color;
        yield return new WaitForSeconds(duration);
        spriteRenderer.color = Color.white;
    }

    // Rapid cyan flashing for the phase 2 transition
    private IEnumerator Phase2Flash()
    {
        for (int i = 0; i < 6; i++)
        {
            spriteRenderer.color = Color.cyan;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
        }
    }
}