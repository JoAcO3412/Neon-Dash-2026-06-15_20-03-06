using System.Collections;
using UnityEngine;
using Platformer.Mechanics;
using Platformer.Core;
using Platformer.Model;
using static Platformer.Core.Simulation;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    public class PlayerController : KinematicObject
    {
        [Header("Audio")]
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        [Header("Movimiento")]
        public float maxSpeed = 5f;
        public float jumpTakeOffSpeed = 10f;

        [Header("Estado")]
        public JumpState jumpState = JumpState.Grounded;
        public Collider2D collider2d;
        public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        [Header("Doble salto")]
        private bool canDoubleJump = false;

        private bool jump;
        private bool stopJump;
        private float moveX = 0f;
        private SpriteRenderer spriteRenderer;
        internal Animator animator;

        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();
        
        private InputAction m_JumpAction;
        private InputAction m_MoveAction;
        private Vector3 spawnPosition;

        public Bounds Bounds => collider2d.bounds;

        void Awake()
        {
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
            if (m_JumpAction != null) m_JumpAction.Enable();

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            if (m_MoveAction != null) m_MoveAction.Enable();

            // Buscar SpawnPoint en la escena
            GameObject spawn = GameObject.Find("SpawnPoint");
            if (spawn != null)
                spawnPosition = spawn.transform.position;
            else
                spawnPosition = transform.position;
        }

        protected override void Update()
        {
            if (transform.position.y < -30f)
            {
                Respawn();
                return;
            }

            if (controlEnabled)
            {
                if (m_JumpAction != null && m_JumpAction.WasPressedThisFrame())
                {
                    if (jumpState == JumpState.Grounded)
                    {
                        jumpState = JumpState.PrepareToJump;
                        canDoubleJump = true;
                    }
                    else if (canDoubleJump)
                    {
                        velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                        canDoubleJump = false;
                        if (audioSource && jumpAudio)
                            audioSource.PlayOneShot(jumpAudio);
                    }
                }
                else if (m_JumpAction != null && m_JumpAction.WasReleasedThisFrame())
                {
                    stopJump = true;
                }
            }

            UpdateJumpState();
            base.Update();
        }

        void UpdateJumpState()
        {
            jump = false;
            switch (jumpState)
            {
                case JumpState.PrepareToJump:
                    jumpState = JumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case JumpState.Jumping:
                    if (!IsGrounded)
                        jumpState = JumpState.InFlight;
                    break;
                case JumpState.InFlight:
                    if (IsGrounded)
                    {
                        jumpState = JumpState.Landed;
                        canDoubleJump = false;
                    }
                    break;
                case JumpState.Landed:
                    jumpState = JumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
                if (audioSource && jumpAudio)
                    audioSource.PlayOneShot(jumpAudio);
            }
            else if (stopJump)
            {
                stopJump = false;
                if (velocity.y > 0)
                    velocity.y = velocity.y * model.jumpDeceleration;
            }

            // Movimiento horizontal
            moveX = 0f;

            if (controlEnabled)
            {
                if (m_MoveAction != null)
                    moveX = m_MoveAction.ReadValue<Vector2>().x;
                else
                    moveX = Input.GetAxis("Horizontal");
            }

            targetVelocity = new Vector2(moveX * maxSpeed, 0);

            // Girar sprite
            if (moveX > 0.01f)
                spriteRenderer.flipX = false;
            else if (moveX < -0.01f)
                spriteRenderer.flipX = true;

            // Animator
            if (animator != null)
            {
                try { animator.SetFloat("velocityX", Mathf.Abs(moveX)); } catch { }
                try { animator.SetBool("grounded", IsGrounded); } catch { }
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Enemy") || other.CompareTag("Obstacle"))
                Respawn();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Enemy") ||
                collision.gameObject.CompareTag("Obstacle"))
                Respawn();
        }

        public void Respawn()
        {
            transform.position = spawnPosition;
            velocity = Vector2.zero;
            jumpState = JumpState.Grounded;
            canDoubleJump = false;

            if (audioSource && respawnAudio)
                audioSource.PlayOneShot(respawnAudio);
        }

        public enum JumpState
        {
            Grounded,
            PrepareToJump,
            Jumping,
            InFlight,
            Landed
        }
    }
}