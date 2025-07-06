using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FormType
{
    Base,
    Echo1,
    Echo2,
    Echo3,
    Echo4
}

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rollSpeed = 8f;
    [SerializeField] private float rollDuration = 0.5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float crouchSpeedModifier = 0.5f;
    
    [Header("Combat Settings")]
    [SerializeField] private float specialAttackCooldown = 3f;
    [SerializeField] private float maxChargeTime = 2f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 3f;
    [SerializeField] private float jumpDuration = 0.5f;
    [SerializeField] private float gravity = -20f;
    
    [Header("Animation Timing")]
    [SerializeField] private float attack1Duration = 0.4f;
    [SerializeField] private float attack2Duration = 0.5f;
    [SerializeField] private float attack3Duration = 0.6f;
    [SerializeField] private float specialAttackDuration = 1.2f;
    [SerializeField] private float rollAnimationDuration = 0.5f;
    [SerializeField] private float hurtAnimationDuration = 0.5f;
    
    [Header("Form System")]
    [SerializeField] private FormType currentForm = FormType.Base;
    [SerializeField] private RuntimeAnimatorController[] formAnimators = new RuntimeAnimatorController[5];
    
    // Movement variables
    private float horizontalInput;
    private float verticalInput;
    private bool facingRight = true;
    private bool isMoving = false;
    private bool isGrounded = true;
    private bool isCrouching = false;
    private float verticalVelocity = 0f;
    private bool isJumpingUp = false;
    
    // Combat variables - Charge System
    private bool isCharging = false;
    private float chargeStartTime;
    private float chargeDuration;
    private bool isAttacking = false;
    private float lastSpecialAttackTime;
    
    // State variables
    private bool isRolling = false;
    private bool isDefending = false;
    private bool isHurt = false;
    private bool canTransform = true;
    private bool isJumping = false;
    
    // Input buffer for smoother controls
    private bool rollInputBuffer = false;
    private bool defendInputBuffer = false;
    private bool specialInputBuffer = false;
    private bool jumpInputBuffer = false;
    
    // Coroutine references to prevent multiple instances
    private Coroutine attackResetCoroutine;
    private Coroutine specialAttackResetCoroutine;
    private Coroutine hurtResetCoroutine;
    
    void Start()
    {
        // Cache components
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Initialize form system
        if (formAnimators.Length > 0 && formAnimators[0] != null)
        {
            animator.runtimeAnimatorController = formAnimators[0];
        }
        
        // Set initial animator parameters
        UpdateAnimatorParameters();
    }
    
    void Update()
    {
        HandleInput();
        HandleMovement();
        HandleChargeAttack();
        HandleFormAbilities();
        HandleFormTransformation();
        UpdateAnimatorParameters();
        ProcessInputBuffers();
    }
    
    void HandleInput()
    {
        // Movement input - WASD
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        
        // Crouch input - Ctrl
        isCrouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        
        // Jump input - Space with buffering
        if (Input.GetKeyDown(KeyCode.Space))
            jumpInputBuffer = true;
            
        // Combat input with buffering for smoother feel
        if (Input.GetKeyDown(KeyCode.X)) // Special Attack
            specialInputBuffer = true;
            
        if (Input.GetKey(KeyCode.LeftShift)) // Defend/Block
            defendInputBuffer = true;
        else
            defendInputBuffer = false;
            
        if (Input.GetKeyDown(KeyCode.Q)) // Roll/Dodge
            rollInputBuffer = true;
    }
    
    void HandleMovement()
    {
        // Don't move if performing certain actions
        if (isAttacking || isRolling || isHurt)
            return;
            
        // Handle jumping
        if (jumpInputBuffer && isGrounded && !isCrouching)
        {
            PerformJump();
            jumpInputBuffer = false;
        }
        
        // Apply custom gravity when not grounded
        if (!isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
            transform.position += Vector3.up * verticalVelocity * Time.deltaTime;
            
            // Check if we should stop jumping animation
            if (isJumpingUp && verticalVelocity <= 0)
            {
                isJumpingUp = false;
                isJumping = false;
            }
        }
        else
        {
            // Reset vertical velocity when grounded
            verticalVelocity = 0f;
            isJumpingUp = false;
            isJumping = false;
        }
            
        // Apply horizontal movement
        float currentSpeed = isCrouching ? moveSpeed * crouchSpeedModifier : moveSpeed;
        Vector2 horizontalMovement = new Vector2(horizontalInput * currentSpeed, 0);
        transform.position += (Vector3)horizontalMovement * Time.deltaTime;
        
        // Update movement state
        isMoving = Mathf.Abs(horizontalInput) > 0.1f;
        
        // Handle sprite flipping
        if (horizontalInput > 0 && !facingRight)
            Flip();
        else if (horizontalInput < 0 && facingRight)
            Flip();
    }
    
    void PerformJump()
    {
        // Calculate jump velocity based on desired height
        float jumpVelocity = Mathf.Sqrt(2 * jumpHeight * Mathf.Abs(gravity));
        verticalVelocity = jumpVelocity;
        
        isGrounded = false;
        isJumping = true;
        isJumpingUp = true;
        animator.SetTrigger("Jump");
    }
    
    void HandleChargeAttack()
    {
        // Start charging attack
        if (Input.GetKeyDown(KeyCode.E) && !isCharging && !isAttacking && !isRolling && !isDefending && !isJumping)
        {
            StartCharging();
        }
        
        // Release attack
        if (Input.GetKeyUp(KeyCode.E) && isCharging)
        {
            ReleaseAttack();
        }
        
        // Update charge duration
        if (isCharging)
        {
            chargeDuration = Time.time - chargeStartTime;
            // Clamp to max charge time
            chargeDuration = Mathf.Min(chargeDuration, maxChargeTime);
        }
    }
    
    void StartCharging()
    {
        isCharging = true;
        chargeStartTime = Time.time;
        chargeDuration = 0f;
        
        // Optional: Add charging animation or effects
        animator.SetBool("isCharging", true);
    }
    
    void ReleaseAttack()
    {
        if (!isCharging) return;
        
        isCharging = false;
        isAttacking = true;
        
        animator.SetBool("isCharging", false);
        
        // Determine attack type based on charge duration
        int attackType = GetAttackType(chargeDuration);
        
        // Stop any existing attack reset coroutine
        if (attackResetCoroutine != null)
            StopCoroutine(attackResetCoroutine);
        
        // Set the appropriate attack trigger
        animator.SetTrigger("Attack" + attackType);
        
        // Reset attack state after appropriate duration
        float attackDuration = GetAttackDuration(attackType);
        
        attackResetCoroutine = StartCoroutine(ResetAttackState(attackDuration));
        
        Debug.Log($"Attack {attackType} - Charge Duration: {chargeDuration:F2}s");
    }
    
    int GetAttackType(float duration)
    {
        if (duration < 0.5f)
            return 1; // Quick attack
        else if (duration < 1f)
            return 2; // Medium attack
        else
            return 3; // Heavy attack
    }
    
    float GetAttackDuration(int attackType)
    {
        switch (attackType)
        {
            case 1: return attack1Duration;
            case 2: return attack2Duration;
            case 3: return attack3Duration;
            default: return attack1Duration;
        }
    }
    
    void HandleFormAbilities()
    {
        // Only non-base forms have special abilities
        if (currentForm == FormType.Base)
            return;
            
        // Roll ability (Q key)
        if (rollInputBuffer && !isRolling && !isAttacking && !isDefending && !isCharging)
        {
            StartCoroutine(PerformRoll());
            rollInputBuffer = false;
        }
        
        // Defend ability (Left Shift)
        if (defendInputBuffer && !isRolling && !isAttacking && !isCharging)
        {
            isDefending = true;
        }
        else
        {
            isDefending = false;
        }
        
        // Special attack (X key)
        if (specialInputBuffer && Time.time - lastSpecialAttackTime > specialAttackCooldown && 
            !isRolling && !isDefending && !isJumping && !isCharging)
        {
            PerformSpecialAttack();
            specialInputBuffer = false;
        }
    }
    
    void HandleFormTransformation()
    {
        if (!canTransform || isCharging)
            return;
            
        // Form transformation inputs (1-5 keys)
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ChangeForm(FormType.Base);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            ChangeForm(FormType.Echo1);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            ChangeForm(FormType.Echo2);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            ChangeForm(FormType.Echo3);
        else if (Input.GetKeyDown(KeyCode.Alpha5))
            ChangeForm(FormType.Echo4);
    }
    
    IEnumerator PerformRoll()
    {
        isRolling = true;
        animator.SetTrigger("Roll");
        
        // Apply roll movement using transform
        Vector2 rollDirection = facingRight ? Vector2.right : Vector2.left;
        float rollTime = 0f;
        
        while (rollTime < rollDuration)
        {
            transform.position += (Vector3)(rollDirection * rollSpeed * Time.deltaTime);
            rollTime += Time.deltaTime;
            yield return null;
        }
        
        isRolling = false;
    }
    
    void PerformSpecialAttack()
    {
        lastSpecialAttackTime = Time.time;
        isAttacking = true;
        
        // Trigger special attack animation
        animator.SetTrigger("SpecialAttack");
        
        // Stop any existing special attack reset coroutine
        if (specialAttackResetCoroutine != null)
            StopCoroutine(specialAttackResetCoroutine);
        
        // Reset after animation
        specialAttackResetCoroutine = StartCoroutine(ResetSpecialAttackState(specialAttackDuration));
    }
    
    IEnumerator ResetAttackState(float duration)
    {
        yield return new WaitForSeconds(duration);
        isAttacking = false;
        attackResetCoroutine = null;
    }
    
    IEnumerator ResetSpecialAttackState(float duration)
    {
        yield return new WaitForSeconds(duration);
        isAttacking = false;
        specialAttackResetCoroutine = null;
    }
    
    public void ChangeForm(FormType newForm)
    {
        if (newForm == currentForm || !canTransform)
            return;
            
        currentForm = newForm;
        
        // Reset all states when changing form
        ResetAllStates();
        
        // Change animator controller
        int formIndex = (int)newForm;
        if (formIndex < formAnimators.Length && formAnimators[formIndex] != null)
        {
            animator.runtimeAnimatorController = formAnimators[formIndex];
        }
        
        // Trigger transformation animation
        animator.SetTrigger("Transform");
        
        // Brief transformation cooldown
        StartCoroutine(TransformationCooldown());
    }
    
    IEnumerator TransformationCooldown()
    {
        canTransform = false;
        yield return new WaitForSeconds(0.5f);
        canTransform = true;
    }
    
    void ResetAllStates()
    {
        // Stop any running coroutines
        if (attackResetCoroutine != null)
        {
            StopCoroutine(attackResetCoroutine);
            attackResetCoroutine = null;
        }
        if (specialAttackResetCoroutine != null)
        {
            StopCoroutine(specialAttackResetCoroutine);
            specialAttackResetCoroutine = null;
        }
        if (hurtResetCoroutine != null)
        {
            StopCoroutine(hurtResetCoroutine);
            hurtResetCoroutine = null;
        }
        
        isAttacking = false;
        isRolling = false;
        isDefending = false;
        isHurt = false;
        isJumping = false;
        isJumpingUp = false;
        isCharging = false;
        chargeDuration = 0f;
        verticalVelocity = 0f;
        
        // Clear input buffers
        rollInputBuffer = false;
        specialInputBuffer = false;
        defendInputBuffer = false;
        jumpInputBuffer = false;
    }
    
    void ProcessInputBuffers()
    {
        // Clear input buffers after a short time to prevent accidental inputs
        if (rollInputBuffer)
        {
            StartCoroutine(ClearInputBuffer("roll", 0.1f));
        }
        if (specialInputBuffer)
        {
            StartCoroutine(ClearInputBuffer("special", 0.1f));
        }
        if (jumpInputBuffer)
        {
            StartCoroutine(ClearInputBuffer("jump", 0.1f));
        }
    }
    
    IEnumerator ClearInputBuffer(string inputType, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        switch (inputType)
        {
            case "roll":
                rollInputBuffer = false;
                break;
            case "special":
                specialInputBuffer = false;
                break;
            case "jump":
                jumpInputBuffer = false;
                break;
        }
    }
    
    void UpdateAnimatorParameters()
    {
        if (animator == null) return;
    
        // Movement parameters
        animator.SetBool("isRunning", isMoving);
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        animator.SetBool("isGrounded", isGrounded);
        animator.SetBool("isCrouching", isCrouching);
        animator.SetBool("isJumping", isJumping);
    
        // Combat parameters
        animator.SetBool("isAttacking", isAttacking);
        animator.SetBool("isCharging", isCharging);
        animator.SetFloat("chargeDuration", chargeDuration);
    
        // State parameters
        animator.SetBool("isRolling", isRolling);
        animator.SetBool("isDefending", isDefending);
        animator.SetBool("isHurt", isHurt);
    
        // Form parameter
        animator.SetInteger("currentForm", (int)currentForm);
    }
    
    void Flip()
    {
        facingRight = !facingRight;
        spriteRenderer.flipX = !facingRight;
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            isJumping = false;
        }
    }
    
    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }
    
    // Public methods for external damage/effect systems
    public void TakeDamage()
    {
        if (isDefending || isRolling) return; // Avoid damage while defending or rolling
        
        isHurt = true;
        
        // Cancel charging if taking damage
        if (isCharging)
        {
            isCharging = false;
            animator.SetBool("isCharging", false);
        }
        
        // Stop any existing hurt reset coroutine
        if (hurtResetCoroutine != null)
            StopCoroutine(hurtResetCoroutine);
        
        hurtResetCoroutine = StartCoroutine(ResetHurtState(hurtAnimationDuration));
    }
    
    IEnumerator ResetHurtState(float duration)
    {
        yield return new WaitForSeconds(duration);
        isHurt = false;
        hurtResetCoroutine = null;
    }
    
    // Getters for other systems
    public FormType GetCurrentForm() => currentForm;
    public bool IsDefending() => isDefending;
    public bool IsRolling() => isRolling;
    public bool IsAttacking() => isAttacking;
    public bool IsCrouching() => isCrouching;
    public bool IsCharging() => isCharging;
    public float GetChargeDuration() => chargeDuration;
    
    // Animation Events (called from animation clips)
    public void OnAttackComplete()
    {
        isAttacking = false;
        
        // Stop the reset coroutine since animation event handles it
        if (attackResetCoroutine != null)
        {
            StopCoroutine(attackResetCoroutine);
            attackResetCoroutine = null;
        }
    }
    
    public void OnSpecialAttackComplete()
    {
        isAttacking = false;
        
        // Stop the reset coroutine since animation event handles it
        if (specialAttackResetCoroutine != null)
        {
            StopCoroutine(specialAttackResetCoroutine);
            specialAttackResetCoroutine = null;
        }
    }
    
    public void OnTransformComplete()
    {
        // Transformation animation finished
        Debug.Log($"Transformed to {currentForm}");
    }
}