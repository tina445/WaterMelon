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
    Animator ani;
    public SpriteRenderer spr;

    float deadtime;

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Balls"))
        {
            Balls other = collision.gameObject.GetComponent<Balls>();

            if (!manager.isEX && level == other.level && !is_merge && !other.is_merge && level < 10)
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
            if (manager.isEX && level == other.level && !is_merge && !other.is_merge && level < 12)
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (manager.play_attach_sound)
        {
            StartCoroutine(Attach_R());
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

    IEnumerator Attach_R()
    {
        if (is_attach)
        {
            yield break;
        }

        is_attach = true;
        manager.sfxPlay(GameManager.sfx.Attach);
        yield return new WaitForSeconds(0.5f);
        is_attach = false;
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
        rgbd.velocity = Vector3.zero;
        rgbd.angularVelocity = 0;
        circle.enabled = true;
    }

    void Update()
    {
        if (is_drag)
        {
            Vector3 movePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float leftborder = -5f + transform.localScale.x * 2f;
            float rightborder = 5f - transform.localScale.x * 2f;

            if (movePos.x < leftborder)
            {
                movePos.x = leftborder;
            }
            else if (movePos.x > rightborder)
            {
                movePos.x = rightborder;
            }

            movePos.y = 6.75f;
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

        manager.score += level + 1;

        is_merge = false;
        gameObject.SetActive(false);
    }

    void level_up()
    {
        is_merge = true;

        rgbd.velocity = Vector2.zero;
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
        manager.sfxPlay(GameManager.sfx.LevelUp);

        yield return new WaitForSeconds(0.3f);
        level++;

        manager.max_level = Mathf.Max(level, manager.max_level);
        manager.spawn_level = Mathf.Max(level, manager.spawn_level);

        if ((!manager.isEX || manager.isHard) && manager.spawn_level > 5)
        {
            manager.spawn_level = 5;
        }
        else if (manager.spawn_level > 7)
        {
            manager.spawn_level = 7;
        }

        is_merge = false;
    }

    void Effect()
    {
        effect.transform.position = transform.position;
        effect.transform.localScale = transform.localScale;
        effect.Play();
    }
}
