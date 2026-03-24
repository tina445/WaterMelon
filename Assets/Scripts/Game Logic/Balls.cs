using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class Balls : MonoBehaviour
{
    public GameManager manager;
    public ParticleSystem effect;

    public int level;
    public bool is_drag;
    public bool is_merge;
    public bool is_attach;

    public Rigidbody2D rgbd;
    CircleCollider2D circle;
    public Animator ani;
    public SpriteRenderer spr;

    float deadtime;

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Balls"))
        {
            Balls other = collision.gameObject.GetComponent<Balls>();

            int maxExclusive = manager.MergeMaxLevelExclusive;
            
            if (level == other.level && !is_merge && !other.is_merge && level < maxExclusive)
            {
                float myX = transform.position.x;
                float myY = transform.position.y;
                float otherX = other.transform.position.x;
                float otherY = other.transform.position.y;

                if (myY < otherY || (myY == otherY && myX > otherX))
                {
                    other.Hide(transform.position);

                    level_up();
                }
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Finish"))
        {
            deadtime += Time.deltaTime;

            if (manager.isHard)
            {
                if (deadtime > 0.5)
                {
                    spr.color = new Color(0.9f, 0.2f, 0.2f);
                    manager.Dead();
                }
            }
            else
            {
                if (deadtime > 2)
                {
                    spr.color = new Color(0.9f, 0.2f, 0.2f);
                }
                if (deadtime > 5)
                {
                    manager.Dead();
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Finish"))
        {
            deadtime = 0;
            spr.color = Color.white;
        }
    }

    void Awake()
    {
        rgbd = GetComponent<Rigidbody2D>();
        ani = GetComponent<Animator>();
        circle = GetComponent<CircleCollider2D>();
        spr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (AppFacade.I != null && AppFacade.I.Skin != null)
        AppFacade.I.Skin.ApplySprite(this, manager != null && manager.isEX);

        if (manager.isEX)
        {
            ani.SetInteger("level_ex", level);
        }
        else if (!manager.isEX)
        {
            ani.SetInteger("level", level);
        }
    }

    void OnDisable()
    {
        level = 0;
        is_drag = false;
        is_merge = false;
        is_attach = false;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.zero;
        rgbd.simulated = false;
        rgbd.linearVelocity = Vector3.zero;
        rgbd.angularVelocity = 0;
        circle.enabled = true;
    }

    void Update()
    {
        if (is_drag)
        {
            Vector3 movePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // 보드 중심 기준으로 경계 계산
            float centerX = manager.transform.position.x;

            float leftborder = centerX - 5f + transform.localScale.x * 2f;
            float rightborder = centerX + 5f - transform.localScale.x * 2f;

            if (movePos.x < leftborder) movePos.x = leftborder;
            else if (movePos.x > rightborder) movePos.x = rightborder;

            movePos.y = 6f;
            movePos.z = 0;
            transform.position = Vector3.Lerp(transform.position, movePos, 0.2f);
        }
    }

    public void Drag()
    {
        is_drag = true;
    }

    public void Drop()
    {
        is_drag = false;
        rgbd.simulated = true;
    }

    public void Hide(Vector3 targetPos)
    {
        is_merge = true;
        rgbd.simulated = false;
        circle.enabled = false;

        if (targetPos == Vector3.up * 1000)
        {
            Effect();
        }

        StartCoroutine(Hide_R(targetPos));
    }

    IEnumerator Hide_R(Vector3 targetPos)
    {
        int frame = 0;
        while (frame < 20)
        {
            frame++;
            if (targetPos != Vector3.up * 1000)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, 0.5f);
            }
            else if (targetPos == Vector3.up * 1000)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 0.2f);
            }

            yield return null;
        }

        manager.AddScore(level + 1);

        is_merge = false;
        gameObject.SetActive(false);
    }

    void level_up()
    {
        is_merge = true;

        rgbd.linearVelocity = Vector2.zero;
        rgbd.angularVelocity = 0;

        StartCoroutine(lvup_R());
    }

    IEnumerator lvup_R()
    {
        yield return new WaitForSeconds(0.2f);

        if (manager.isEX)
        {
            ani.SetInteger("level_ex", level + 1);
        }
        else if (!manager.isEX)
        {
            ani.SetInteger("level", level + 1);
        }

        Effect();
        AppFacade.I.Audio.PlayOneShot(AudioService.sfx.LevelUp);

        yield return new WaitForSeconds(0.3f);
        level++;

        // 레벨 바뀌었으니 스킨 스프라이트 갱신
        if (AppFacade.I != null && AppFacade.I.Skin != null)
        AppFacade.I.Skin.ApplySprite(this, manager != null && manager.isEX);

        manager.max_level = Mathf.Max(level, manager.max_level);
        manager.spawn_level = Mathf.Max(level, manager.spawn_level);

        // 기존 규칙을 전략 기반 clamp로 통일
        manager.ApplySpawnClamp();

        is_merge = false;
    }

    void Effect()
    {
        effect.transform.position = transform.position;
        effect.transform.localScale = transform.localScale;
        effect.Play();
    }
}
