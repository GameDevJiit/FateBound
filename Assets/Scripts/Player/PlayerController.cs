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
    
    [Header("Combat Settings")]
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float comboCooldown = 1.5f;
    [SerializeField] private float specialAttackCooldown = 3f;
    
    [Header("Form System")]
    [SerializeField] private FormType currentForm = FormType.Base;
    [SerializeField] private RuntimeAnimatorController[] formAnimators = new RuntimeAnimatorController[5];
    
    // Movement variables
    private float horizontalInput;
    private bool facingRight = true;
    private bool isMoving = false;
    
    // Combat variables
    private int attackPhase = 0;
    private bool canAttack = true;
    private bool isAttacking = false;
    private float lastAttackTime;
    private float lastSpecialAttackTime;
    
    // State variables
    private bool isRolling = false;
    private bool isDefending = false;
    private bool isHurt = false;
    private bool canTransform = true;
    
    // Input buffer for smoother controls
    private bool attackInputBuffer = false;
    private bool rollInputBuffer = false;
    private bool defendInputBuffer = false;
    private bool specialInputBuffer = false;
    
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
        HandleAttack();
        HandleFormAbilities();
        HandleFormTransformation();
        UpdateAnimatorParameters();
        ProcessInputBuffers();
    }
    
    void HandleInput()
    {
        // Movement input
        horizontalInput = Input.GetAxisRaw("Horizontal");
        
        // Combat input with buffering for smoother feel
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.J))
            attackInputBuffer = true;
            
        if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.K))
            rollInputBuffer = true;
            
        if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.L))
            defendInputBuffer = true;
        else
            defendInputBuffer = false;
            
        if (Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.I))
            specialInputBuffer = true;
    }
    
    void HandleMovement()
    {
        // Don't move if performing certain actions
        if (isAttacking || isRolling || isDefending)
            return;
            
        // Apply movement
        Vector2 movement = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);
        rb.velocity = movement;
        
        // Update movement state
        isMoving = Mathf.Abs(horizontalInput) > 0.1f;
        
        // Handle sprite flipping
        if (horizontalInput > 0 && !facingRight)
            Flip();
        else if (horizontalInput < 0 && facingRight)
            Flip();
    }
    
    void HandleAttack()
    {
        // Reset combo if too much time passed
        if (Time.time - lastAttackTime > comboCooldown)
        {
            attackPhase = 0;
        }
        
        // Process attack input
        if (attackInputBuffer && canAttack && !isRolling && !isDefending)
        {
            PerformAttack();
            attackInputBuffer = false;
        }
    }
    
    void HandleFormAbilities()
    {
        // Only non-base forms have special abilities
        if (currentForm == FormType.Base)
            return;
            
        // Roll ability
        if (rollInputBuffer && !isRolling && !isAttacking && !isDefending)
        {
            StartCoroutine(PerformRoll());
            rollInputBuffer = false;
        }
        
        // Defend ability
        if (defendInputBuffer && !isRolling && !isAttacking)
        {
            isDefending = true;
        }
        else
        {
            isDefending = false;
        }
        
        // Special attack
        if (specialInputBuffer && Time.time - lastSpecialAttackTime > specialAttackCooldown && 
            !isRolling && !isDefending)
        {
            PerformSpecialAttack();
            specialInputBuffer = false;
        }
    }
    
    void HandleFormTransformation()
    {
        if (!canTransform)
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
    
    void PerformAttack()
    {
        isAttacking = true;
        canAttack = false;
        
        // Advance combo phase
        attackPhase = (attackPhase % 3) + 1; // Cycles through 1, 2, 3
        
        lastAttackTime = Time.time;
        
        // Stop movement during attack
        rb.velocity = new Vector2(0, rb.velocity.y);
        
        // Stop any existing attack reset coroutine
        if (attackResetCoroutine != null)
            StopCoroutine(attackResetCoroutine);
        
        // Reset attack state after animation
        attackResetCoroutine = StartCoroutine(ResetAttackState());
    }
    
    IEnumerator PerformRoll()
    {
        isRolling = true;
        
        // Apply roll velocity
        Vector2 rollDirection = facingRight ? Vector2.right : Vector2.left;
        rb.velocity = new Vector2(rollDirection.x * rollSpeed, rb.velocity.y);
        
        yield return new WaitForSeconds(rollDuration);
        
        isRolling = false;
        rb.velocity = new Vector2(0, rb.velocity.y);
    }
    
    void PerformSpecialAttack()
    {
        lastSpecialAttackTime = Time.time;
        isAttacking = true;
        
        // Stop movement during special attack
        rb.velocity = new Vector2(0, rb.velocity.y);
        
        // Trigger special attack animation
        animator.SetTrigger("SpecialAttack");
        
        // Stop any existing special attack reset coroutine
        if (specialAttackResetCoroutine != null)
            StopCoroutine(specialAttackResetCoroutine);
        
        // Reset after animation
        specialAttackResetCoroutine = StartCoroutine(ResetSpecialAttackState());
    }
    
    IEnumerator ResetAttackState()
    {
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
        canAttack = true;
        attackResetCoroutine = null;
    }
    
    IEnumerator ResetSpecialAttackState()
    {
        yield return new WaitForSeconds(1f); // Adjust based on animation length
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
        attackPhase = 0;
        canAttack = true;
        
        // Clear input buffers
        attackInputBuffer = false;
        rollInputBuffer = false;
        specialInputBuffer = false;
        defendInputBuffer = false;
        
        // Stop movement
        rb.velocity = new Vector2(0, rb.velocity.y);
    }
    
    void ProcessInputBuffers()
    {
        // Clear input buffers after a short time to prevent accidental inputs
        if (attackInputBuffer)
        {
            StartCoroutine(ClearInputBuffer("attack", 0.1f));
        }
        if (rollInputBuffer)
        {
            StartCoroutine(ClearInputBuffer("roll", 0.1f));
        }
        if (specialInputBuffer)
        {
            StartCoroutine(ClearInputBuffer("special", 0.1f));
        }
    }
    
    IEnumerator ClearInputBuffer(string inputType, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        switch (inputType)
        {
            case "attack":
                attackInputBuffer = false;
                break;
            case "roll":
                rollInputBuffer = false;
                break;
            case "special":
                specialInputBuffer = false;
                break;
        }
    }
    
    void UpdateAnimatorParameters()
    {
        if (animator == null) return;
    
        // Movement parameters
        animator.SetBool("isRunning", isMoving);
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
    
        // Combat parameters
        animator.SetInteger("attackPhase", attackPhase);
        animator.SetBool("isAttacking", isAttacking);
    
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
    
    // Public methods for external damage/effect systems
    public void TakeDamage()
    {
        if (isDefending || isRolling) return; // Avoid damage while defending or rolling
        
        isHurt = true;
        
        // Stop any existing hurt reset coroutine
        if (hurtResetCoroutine != null)
            StopCoroutine(hurtResetCoroutine);
        
        hurtResetCoroutine = StartCoroutine(ResetHurtState());
    }
    
    IEnumerator ResetHurtState()
    {
        yield return new WaitForSeconds(0.5f);
        isHurt = false;
        hurtResetCoroutine = null;
    }
    
    // Getters for other systems
    public FormType GetCurrentForm() => currentForm;
    public bool IsDefending() => isDefending;
    public bool IsRolling() => isRolling;
    public bool IsAttacking() => isAttacking;
    
    // Animation Events (called from animation clips)
    public void OnAttackComplete()
    {
        isAttacking = false;
        canAttack = true;
        
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