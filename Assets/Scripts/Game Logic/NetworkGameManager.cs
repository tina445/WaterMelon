using UnityEngine;

public enum BoardRole
{
    None,
    Host,
    Client
}

public class NetworkGameManager : GameManager
{
    [Header("Board Role")]
    [SerializeField] private BoardRole boardRole = BoardRole.None;
    public BoardRole Role => boardRole;

    [Header("Remote View")]
    [SerializeField] private bool isRemoteView = false;
    public bool IsRemoteView => isRemoteView;

    [Header("Spawn (Local Space)")]
    [SerializeField] private float spawnLocalY = 6f;
    [SerializeField] private float spawnLocalX = 0f;

    public void SetRemoteView(bool remote)
    {
        isRemoteView = remote;

        if (isRemoteView)
        {
            is_play = false;
            is_over = false;

            CancelInvoke();
            StopAllCoroutines();

            for (int i = 0; i < pool_ball.Count; i++)
            {
                var b = pool_ball[i];
                if (b == null) continue;
                if (b.rgbd) b.rgbd.simulated = false;
                b.is_drag = false;
            }
        }
    }

    protected override void Start()
    {
        if (isRemoteView)
        {
            SetRemoteView(true);
            return;
        }
        base.Start();
    }

    protected override void next()
    {
        if (is_over) return;

        lastball = get_ball();
        lastball.level = UnityEngine.Random.Range(0, spawn_level);
        lastball.gameObject.SetActive(true);

        Vector3 spawnWorld = transform.TransformPoint(new Vector3(spawnLocalX, spawnLocalY, 0f));
        lastball.transform.position = spawnWorld;

        AppFacade.I.Audio.PlayOneShot(AudioService.sfx.Next);
        StartCoroutine(next_waiting());
    }

    public override void Dead()
    {
        if (is_over) return;

        is_over = true;
        is_play = false;

        CancelInvoke();
        StopAllCoroutines();

        for (int i = 0; i < pool_ball.Count; i++)
        {
            var b = pool_ball[i];
            if (b == null) continue;
            if (b.rgbd) b.rgbd.simulated = false;
            b.is_drag = false;
        }
    }
}
