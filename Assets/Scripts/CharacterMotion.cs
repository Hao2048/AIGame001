using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Generic skeletal animation, driven by the existing boss state machine.</summary>
[DefaultExecutionOrder(30)]
public sealed class CharacterMotion : MonoBehaviour
{
    public BossController controller;
    readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
    readonly Dictionary<string, Quaternion> _rest = new Dictionary<string, Quaternion>();
    readonly Dictionary<string, Vector3> _curlAxes = new Dictionary<string, Vector3>();
    SkinnedMeshRenderer _face;
    int _blink = -1;
    Transform _phone, _groundContact;
    Vector3 _basePosition;
    float _walkWeight, _phoneWeight, _lectureWeight, _watchWeight, _phase;
    bool _review;
    BossController.BState _reviewState;
    float _reviewClock;
    Vector3 _lastRootPosition;
    float _lastYaw, _speed, _yawRate, _phoneProgress;
    bool _travelInitialized;
    sealed class FootPlant
    {
        public Vector3 neutral, anchor, takeoff, landing;
        public Quaternion restRotation, plantedRotation, takeoffRotation;
        public bool swinging, ready;
    }
    readonly FootPlant _leftFoot = new FootPlant(), _rightFoot = new FootPlant();
    public float TravelSpeed => _speed;
    public float TurnSpeed => _yawRate;
    public bool LeftFootPlanted => _leftFoot.ready && !_leftFoot.swinging && _walkWeight > .5f;
    public bool RightFootPlanted => _rightFoot.ready && !_rightFoot.swinging && _walkWeight > .5f;
    Quaternion _handRRest, _phoneHandRotation;
    float _leftDrop=32f,_rightDrop=-32f,_rightSide=1f;
    public float WalkBlend => _walkWeight;
    public float PhoneBlend => _phoneWeight;

    public void Initialize(BossController boss)
    {
        controller = boss;
        _groundContact = boss.transform.Find("ToonGroundContact");
        _basePosition = transform.localPosition;
        _bones.Clear(); _rest.Clear();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            _bones[t.name] = t; _rest[t.name] = t.localRotation;
            if (t.name == "PhoneProp") _phone = t;
        }
        // Bind to the downloaded Renderpeople skeleton without renaming or re-skinning it.
        Alias("Hips","hip"); Alias("Chest","spine_03"); Alias("Neck","neck"); Alias("Head","head");
        foreach(string side in new[]{"L","R"})
        {
            string suffix=side=="L"?"_l":"_r";
            Alias("UpperArm"+side,"upperarm"+suffix); Alias("Forearm"+side,"lowerarm"+suffix);
            Alias("Hand"+side,"hand"+suffix); Alias("Thigh"+side,"upperleg"+suffix);
            Alias("Shin"+side,"lowerleg"+suffix); Alias("Foot"+side,"foot"+suffix);
            foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})
                for(int k=1;k<=3;k++) Alias(finger+side+k,finger.ToLower()+"_0"+k+suffix);
        }
        _leftDrop=ArmDrop("UpperArmL","ForearmL"); _rightDrop=ArmDrop("UpperArmR","ForearmR");
        if(_bones.TryGetValue("UpperArmR",out var rightShoulder)) _rightSide=Mathf.Sign(transform.InverseTransformPoint(rightShoulder.position).x);
        foreach (string side in new[] { "L", "R" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
                for (int segment = 1; segment <= 3; segment++)
                {
                    string name = finger + side + segment;
                    if (_bones.TryGetValue(name, out var joint))
                        _curlAxes[name] = joint.InverseTransformDirection(transform.forward * (side == "R" ? -_rightSide : _rightSide));
                }
        if(_phone==null && _bones.TryGetValue("HandR",out var phoneHand))
        {
            var phone=new GameObject("PhoneProp",typeof(MeshFilter),typeof(MeshRenderer));
            phone.GetComponent<MeshFilter>().sharedMesh=Resources.Load<GameObject>("Realism/BevelledUnit").GetComponentInChildren<MeshFilter>().sharedMesh;
            phone.GetComponent<MeshRenderer>().sharedMaterial=RealismLook.Surface("Glasses");
            phone.transform.position=phoneHand.position+transform.forward*.035f;
            phone.transform.rotation=transform.rotation;phone.transform.SetParent(phoneHand,true);
            Vector3 parentScale=phoneHand.lossyScale;
            phone.transform.localScale=new Vector3(.064f/parentScale.x,.16f/parentScale.y,.010f/parentScale.z);
            _phone=phone.transform;
        }
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                if (renderer.sharedMesh.GetBlendShapeName(i).Contains("Blink")) { _face = renderer; _blink = i; }
        }
        if (_bones.TryGetValue("HandR",out var rightHand)) _handRRest = Quaternion.Inverse(transform.rotation) * rightHand.rotation;
        _phoneHandRotation=_handRRest;
        if(_bones.TryGetValue("HandR",out var wrist) && _bones.TryGetValue("IndexR1",out var index))
        {
            Vector3 finger=transform.InverseTransformDirection(index.position-wrist.position).normalized;
            Vector3 gripDirection = new Vector3(-_rightSide * .45f, .893f, 0f).normalized;
            _phoneHandRotation = Quaternion.AngleAxis(180f, gripDirection) * Quaternion.FromToRotation(finger, gripDirection) * _handRRest;
        }
        CacheFoot(_leftFoot, "FootL"); CacheFoot(_rightFoot, "FootR");
        _lastRootPosition = controller.transform.position;
        _lastYaw = controller.transform.eulerAngles.y;
        _travelInitialized = false;
        if (_phone != null) _phone.gameObject.SetActive(false);
        Apply(0f, BossController.BState.Watch, false, 0f, true);
    }

    void Alias(string target,string source)
    {
        if(_bones.TryGetValue(source,out var bone)) { _bones[target]=bone; _rest[target]=_rest[source]; }
    }

    float ArmDrop(string upperName,string lowerName)
    {
        if(!_bones.TryGetValue(upperName,out var upper)||!_bones.TryGetValue(lowerName,out var lower))return 0f;
        Vector3 direction=transform.InverseTransformDirection(lower.position-upper.position);
        direction.z=0f;
        return Vector3.SignedAngle(direction,Vector3.down,Vector3.forward);
    }

    public void Preview(BossController.BState state, float seconds = 0f, bool hold = false)
    {
        _review = hold; _reviewState = state; _reviewClock = seconds;
        Apply(seconds, state, state == BossController.BState.Patrol || state == BossController.BState.Approach || state == BossController.BState.Leave, 1f, true);
    }

    void LateUpdate()
    {
        if (controller == null) return;
        if (_review)
        {
            _reviewClock += Time.deltaTime;
            bool walking = _reviewState == BossController.BState.Patrol || _reviewState == BossController.BState.Approach || _reviewState == BossController.BState.Leave;
            Apply(_reviewClock,_reviewState,walking,Time.deltaTime,false);
        }
        else Apply(Time.time, controller.State, controller.IsWalking, Time.deltaTime, false);
    }

    void Pose(string name, float pitch, float yaw = 0f, float roll = 0f)
    {
        if (!_bones.TryGetValue(name, out var bone) || bone == null) return;
        // Rotations are expressed in character space, independent of FBX bone roll.
        Quaternion space = Quaternion.Inverse(bone.parent.rotation) * transform.rotation;
        bone.localRotation = space * Quaternion.Euler(pitch, yaw, roll) * Quaternion.Inverse(space) * _rest[name];
    }

    void Reach(string upperName,string lowerName,string handName,Vector3 targetLocal,Vector3 hintLocal,float weight)
    {
        if (!_bones.TryGetValue(upperName,out var upper) || !_bones.TryGetValue(lowerName,out var lower) || !_bones.TryGetValue(handName,out var hand)) return;
        Vector3 target=transform.TransformPoint(targetLocal),hint=transform.TransformPoint(hintLocal);
        Vector3 shoulder=upper.position, direction=target-shoulder;
        float a=Vector3.Distance(shoulder,lower.position),b=Vector3.Distance(lower.position,hand.position);
        bool phoneArm = upperName == "UpperArmR";
        // Keep the shirt sleeve out of a near-180 degree fold at the elbow.
        float minimum = phoneArm ? Mathf.Sqrt(a*a+b*b+2f*a*b*Mathf.Cos(155f*Mathf.Deg2Rad)) : .01f;
        float distance=Mathf.Clamp(direction.magnitude,minimum,a+b-.002f);
        direction.Normalize();
        target = shoulder + direction * distance;
        Vector3 bend=Vector3.ProjectOnPlane(hint-shoulder,direction).normalized;
        float cos=Mathf.Clamp((a*a+distance*distance-b*b)/(2f*a*distance),-1f,1f);
        Vector3 elbow=shoulder+direction*(cos*a)+bend*(Mathf.Sqrt(1f-cos*cos)*a);
        if (phoneArm)
        {
            // Renderpeople's right arm aims along local X and bends around local Z.
            // Solve its hinge frame explicitly, avoiding arbitrary roll from repeated FromToRotation calls.
            Vector3 upperDirection = (elbow - shoulder).normalized;
            Vector3 lowerDirection = (target - elbow).normalized;
            Vector3 hinge = Vector3.Cross(upperDirection, lowerDirection).normalized;
            upper.rotation = Quaternion.Slerp(upper.rotation,
                Quaternion.LookRotation(hinge, Vector3.Cross(hinge, upperDirection)), weight);
            lower.rotation = Quaternion.Slerp(lower.rotation,
                Quaternion.LookRotation(hinge, Vector3.Cross(hinge, lowerDirection)), weight);
        }
        else
        {
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-shoulder,elbow-shoulder)*upper.rotation,weight);
            lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation,weight);
        }
    }

    void DistributeWristTwist(string side)
    {
        if (!_bones.TryGetValue("Hand" + side, out var hand) ||
            !_bones.TryGetValue("lowerarm_twist_" + side.ToLower(), out var twist)) return;
        Quaternion delta = hand.localRotation * Quaternion.Inverse(_rest["Hand" + side]);
        Quaternion roll = new Quaternion(delta.x, 0f, 0f, delta.w);
        float magnitude = Mathf.Sqrt(roll.x * roll.x + roll.w * roll.w);
        if (magnitude > .0001f) roll = new Quaternion(roll.x / magnitude, 0f, 0f, roll.w / magnitude);
        else roll = Quaternion.identity;
        twist.localRotation = Quaternion.Slerp(Quaternion.identity, roll, .5f) * _rest[twist.name];
    }

    void CacheFoot(FootPlant plant, string name)
    {
        if (!_bones.TryGetValue(name, out var foot)) return;
        plant.neutral = transform.InverseTransformPoint(foot.position);
        plant.restRotation = Quaternion.Inverse(transform.rotation) * foot.rotation;
        plant.ready = false;
    }

    void Step(FootPlant plant, string side, float cycle, float dt, bool instant)
    {
        var root = controller.transform;
        const float stance = .55f;
        bool swinging = cycle >= stance;
        Vector3 neutral = root.TransformPoint(_basePosition + plant.neutral);
        if (!plant.ready || instant)
        {
            plant.anchor = plant.takeoff = plant.landing = neutral;
            plant.plantedRotation = plant.takeoffRotation = root.rotation;
            plant.swinging = swinging;
            plant.ready = true;
        }
        if (swinging && !plant.swinging)
        {
            plant.takeoff = plant.anchor;
            plant.takeoffRotation = plant.plantedRotation;
        }
        float stepLength = Mathf.Lerp(.05f, .31f, Mathf.Clamp01(_speed / 1.4f));
        Vector3 landing = neutral + root.forward * stepLength;
        if (swinging) plant.landing = landing;
        if (!swinging && plant.swinging)
        {
            plant.anchor = plant.landing;
            plant.plantedRotation = root.rotation;
            if (!instant && controller.game != null)
            {
                float distance = OfficeBuilder.Cam != null ? Vector3.Distance(root.position, OfficeBuilder.Cam.transform.position) : 3f;
                controller.game.PlayFootstep(Mathf.Clamp01(1.4f - distance / 6f) * .75f);
            }
        }
        plant.swinging = swinging;
        float swing = Mathf.Clamp01((cycle - stance) / (1f - stance));
        float arc = Mathf.Sin(swing * Mathf.PI);
        Vector3 target = swinging
            ? Vector3.Lerp(plant.takeoff, plant.landing, Mathf.SmoothStep(0f, 1f, swing)) + Vector3.up * arc * .075f
            : plant.anchor;
        // Fade into and out of planted feet without pulling a stopped character off balance.
        target = Vector3.Lerp(neutral, target, _walkWeight);
        Vector3 hint = transform.InverseTransformPoint(root.TransformPoint(plant.neutral + Vector3.up * .40f + Vector3.forward * .55f));
        Reach("Thigh" + side, "Shin" + side, "Foot" + side,
            transform.InverseTransformPoint(target), hint, _walkWeight);
        if (_bones.TryGetValue("Foot" + side, out var foot))
        {
            float roll = swinging ? Mathf.Lerp(12f, -5f, swing) : Mathf.Lerp(-5f, 7f, Mathf.Pow(cycle / stance, 3f));
            Quaternion facing = swinging ? Quaternion.Slerp(plant.takeoffRotation, root.rotation, swing) : plant.plantedRotation;
            Quaternion rotation = Quaternion.AngleAxis(roll, facing * Vector3.right) * facing * plant.restRotation;
            foot.rotation = Quaternion.Slerp(foot.rotation, rotation, _walkWeight);
        }
    }

    void Apply(float t, BossController.BState state, bool walking, float dt, bool instant)
    {
        dt = Mathf.Max(0f, dt);
        var root = controller.transform;
        if (_groundContact != null)
        {
            _groundContact.position = root.position + Vector3.up * .012f;
            _groundContact.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        Vector3 travel = root.position - _lastRootPosition;
        travel.y = 0f;
        float yawDelta = Mathf.DeltaAngle(_lastYaw, root.eulerAngles.y);
        bool resetTravel = !_travelInitialized || travel.magnitude > 1f || state == BossController.BState.Hidden;
        if (resetTravel) { travel = Vector3.zero; yawDelta = 0f; _leftFoot.ready = _rightFoot.ready = false; }
        _lastRootPosition = root.position; _lastYaw = root.eulerAngles.y; _travelInitialized = true;
        float blend = instant ? 1f : 1f - Mathf.Exp(-dt * 10f);
        float measuredSpeed = dt > .0001f ? travel.magnitude / dt : _speed;
        float measuredTurn = dt > .0001f ? yawDelta / dt : _yawRate;
        if (_review) measuredSpeed = walking ? 1.45f : 0f;
        _speed = Mathf.Lerp(_speed, measuredSpeed, blend);
        _yawRate = Mathf.Lerp(_yawRate, measuredTurn, blend);
        float pivot = Mathf.Clamp01(Mathf.Abs(_yawRate) / 100f) * .55f;
        float gait = Mathf.Max(Mathf.Clamp01(_speed / 1.1f), pivot);
        _walkWeight = Mathf.Lerp(_walkWeight, state == BossController.BState.Hidden ? 0f : gait, blend);
        _phoneProgress = instant ? (state == BossController.BState.PhoneCall ? 1f : 0f)
            : Mathf.MoveTowards(_phoneProgress, state == BossController.BState.PhoneCall ? 1f : 0f, dt / (state == BossController.BState.PhoneCall ? .95f : .75f));
        _phoneWeight = Mathf.SmoothStep(0f, 1f, _phoneProgress);
        _lectureWeight = Mathf.Lerp(_lectureWeight, state == BossController.BState.Lecture ? 1f : 0f, blend);
        _watchWeight = Mathf.Lerp(_watchWeight, state == BossController.BState.Warn || state == BossController.BState.Watch ? 1f : 0f, blend);
        if (instant) _phase = t * 2f * Mathf.PI * 1.45f / 1.22f;
        else
        {
            float distance = _review && walking ? 1.45f * dt : travel.magnitude;
            // Include small alternating repositioning steps during a turn on the spot.
            _phase += (distance + Mathf.Abs(yawDelta) * Mathf.Deg2Rad * .16f) / 1.22f * Mathf.PI * 2f;
        }
        float stride = Mathf.Sin(_phase) * _walkWeight;
        float breath = Mathf.Sin(t * 1.65f);
        float turn = Mathf.Clamp(_yawRate / 180f, -1f, 1f);
        float facing = _review ? 0f : Mathf.Clamp(controller.FacingError, -55f, 55f);
        float listen = _phoneWeight * _phoneWeight;
        transform.localPosition = _basePosition + new Vector3(stride * .012f,
            (-.072f + Mathf.Cos(_phase * 2f) * .012f) * _walkWeight + breath * .0015f, 0f);

        Pose("Hips", _walkWeight * 2f, -stride * 3f - turn * 5f, -stride * 1.8f + turn * 2f);
        Pose("Chest", _lectureWeight * 5f - _watchWeight * 2f + breath * .65f,
            stride * 2.3f + facing * .20f - turn * 3f + listen * 3f, breath * .3f - turn * 2f);
        Pose("Neck", _watchWeight * 1f + listen * 2f, facing * .12f);
        float scan = state == BossController.BState.Patrol ? Mathf.Sin(t * .75f) * 5f : state == BossController.BState.Feint ? Mathf.Sin(t * 2.2f) * 10f : 0f;
        Pose("Head", breath * .5f + _lectureWeight * Mathf.Sin(t * 6f) * 3f + listen * (1.5f + Mathf.Sin(t * 2.1f) * 1.2f),
            scan + facing * .43f - listen * 6f, -listen * 3f);
        Pose("ThighL", 0f); Pose("ThighR", 0f);
        Pose("ShinL", 0f); Pose("ShinR", 0f);
        Pose("FootL", 0f); Pose("FootR", 0f);
        if (_walkWeight > .005f)
        {
            Step(_leftFoot, "L", Mathf.Repeat(_phase / (2f * Mathf.PI), 1f), dt, instant);
            Step(_rightFoot, "R", Mathf.Repeat(_phase / (2f * Mathf.PI) + .5f, 1f), dt, instant);
        }
        else _leftFoot.ready = _rightFoot.ready = false;

        // Elbows and hands trail the shoulder swing rather than moving as a rigid lever.
        float armSwing = Mathf.Sin(_phase - .18f) * _walkWeight;
        Pose("UpperArmL", -armSwing * 13f - listen * (3f + Mathf.Sin(t * 1.4f) * 2f), -turn * 4f, _leftDrop);
        Pose("ForearmL", -7f - (1f + Mathf.Sin(_phase - .5f)) * 5f * _walkWeight - listen * 5f);
        Pose("UpperArmR", armSwing * 13f - _lectureWeight * 38f, _lectureWeight * -8f + turn * 4f, _rightDrop);
        Pose("ForearmR", -7f - (1f - Mathf.Sin(_phase - .5f)) * 5f * _walkWeight - _lectureWeight * (42f + Mathf.Sin(t * 5f) * 8f));
        Pose("HandR", _walkWeight * Mathf.Sin(_phase - .65f) * 3f, _lectureWeight * 12f);
        Pose("HandL", -_walkWeight * Mathf.Sin(_phase - .65f) * 3f);
        foreach (string side in new[] { "L", "R" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
                for (int segment = 1; segment <= 3; segment++)
                {
                    float curl = (finger == "Thumb" ? 5f : 8f + segment * 2f) + _walkWeight * 3f;
                    float gripCurl = finger == "Thumb" ? 22f : segment == 1 ? -4f : segment == 2 ? 8f : 14f;
                    if (side == "R") curl += Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_phoneProgress / .28f)) * gripCurl + _lectureWeight * (finger == "Index" ? -8f : 38f);
                    string name = finger + side + segment;
                    if (_bones.TryGetValue(name, out var joint) && _curlAxes.TryGetValue(name, out var axis))
                        joint.localRotation = _rest[name] * Quaternion.AngleAxis(finger == "Thumb" ? curl * .35f : curl, axis);
                }
        if (_phoneProgress > .001f)
        {
            // Pocket -> curved lift in front of the chest -> ear. Reverse the same arc to put it away.
            float u = _phoneWeight;
            Vector3 pocket = new Vector3(_rightSide * .18f, .98f, .06f);
            Vector3 arc = new Vector3(_rightSide * .30f, 1.26f, .34f);
            Vector3 ear = new Vector3(_rightSide * .20f, 1.47f + listen * Mathf.Sin(t * 2.1f) * .002f, .08f);
            Vector3 target = (1f-u)*(1f-u)*pocket + 2f*(1f-u)*u*arc + u*u*ear;
            float engage = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_phoneProgress / .18f));
            Reach("UpperArmR", "ForearmR", "HandR", target,
                new Vector3(_rightSide * .40f, 1.05f + u * .10f, .20f), engage);
            if (_bones.TryGetValue("HandR", out var hand))
            {
                Quaternion wrist = Quaternion.Slerp(_handRRest, _phoneHandRotation, Mathf.SmoothStep(0f, 1f, u));
                hand.rotation = Quaternion.Slerp(hand.rotation, transform.rotation * wrist, engage);
            }
        }
        DistributeWristTwist("R");
        DistributeWristTwist("L");
        if (_phone != null)
        {
            _phone.gameObject.SetActive(_phoneProgress > .08f);
            if (_bones.TryGetValue("HandR", out var grip))
            {
                Quaternion hold = grip.rotation * Quaternion.Inverse(_phoneHandRotation);
                _phone.position = grip.position + hold * new Vector3(-_rightSide * .05f, .095f, -.025f);
                _phone.rotation = hold * Quaternion.Euler(0f, 90f, 0f);
            }
        }
        if (_face != null && _blink >= 0)
        {
            float cycle = Mathf.Repeat(t + 1.7f, 4.3f);
            _face.SetBlendShapeWeight(_blink, Mathf.Clamp01(1f - Mathf.Abs(cycle - .1f) / .085f) * 100f);
        }
    }
}
