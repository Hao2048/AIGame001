using UnityEngine;

/// <summary>Original, redistributable cartoon stand-in. Requires no downloaded character or paid assets.</summary>
public static class BuiltInBoss
{
    static Material ColorMaterial(string name,Color color)
    {
        var material=RealismLook.Flat(color);material.name="OriginalBoss-"+name;
        material.SetColor("_BaseColor",color);return material;
    }
    static Transform Bone(Transform parent,string name,Vector3 position,Quaternion? rotation=null)
    {
        var bone=new GameObject(name).transform;bone.SetParent(parent,false);
        bone.localPosition=position;bone.localRotation=rotation??Quaternion.identity;return bone;
    }
    static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion? rotation=null)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation??Quaternion.identity;
        var collider=go.GetComponent<Collider>();Object.DestroyImmediate(collider);
        if(type==PrimitiveType.Cube)
        {
            var rounded=Resources.Load<GameObject>("Realism/BevelledUnit");
            if(rounded!=null)go.GetComponent<MeshFilter>().sharedMesh=rounded.GetComponentInChildren<MeshFilter>().sharedMesh;
        }
        go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    static void Limb(Transform parent,string name,Vector3 direction,float length,float radius,Material material)
    {
        Shape(parent,name,PrimitiveType.Capsule,direction*length*.5f,
            new Vector3(radius*2f,length*.5f,radius*2f),material,Quaternion.FromToRotation(Vector3.up,direction));
        Shape(parent,name+"Joint",PrimitiveType.Sphere,direction*length,Vector3.one*radius*1.8f,material);
    }

    public static GameObject Create(Transform parent)
    {
        var root=new GameObject("BuiltInCartoonBoss");root.transform.SetParent(parent,false);
        var suit=ColorMaterial("Suit",new Color(.055f,.085f,.13f));
        var shirt=ColorMaterial("Shirt",new Color(.95f,.95f,.90f));
        var skin=ColorMaterial("Skin",new Color(.88f,.64f,.44f));
        var hair=ColorMaterial("Hair",new Color(.07f,.065f,.075f));
        var white=ColorMaterial("EyeWhite",new Color(.98f,.98f,.94f));
        var ink=ColorMaterial("Ink",new Color(.025f,.035f,.05f));
        var tie=ColorMaterial("Tie",new Color(.46f,.055f,.055f));
        var shoe=ColorMaterial("Shoes",new Color(.04f,.055f,.07f));

        var hips=Bone(root.transform,"Hips",new Vector3(0f,.93f,0f));
        var chest=Bone(hips,"Chest",new Vector3(0f,.39f,0f));
        Shape(hips,"Waist",PrimitiveType.Cube,new Vector3(0f,-.015f,0f),new Vector3(.34f,.20f,.23f),suit);
        Shape(chest,"ShirtBody",PrimitiveType.Capsule,new Vector3(0f,-.12f,0f),new Vector3(.40f,.27f,.25f),shirt);
        Shape(chest,"Waistcoat",PrimitiveType.Cube,new Vector3(0f,-.13f,.035f),new Vector3(.39f,.47f,.24f),suit);
        Shape(chest,"ShirtFront",PrimitiveType.Cube,new Vector3(0f,.015f,.165f),new Vector3(.115f,.20f,.025f),shirt);
        Shape(chest,"Tie",PrimitiveType.Cube,new Vector3(0f,-.03f,.184f),new Vector3(.043f,.23f,.022f),tie,Quaternion.Euler(0f,0f,-3f));
        for(int i=0;i<3;i++)Shape(chest,"VestButton",PrimitiveType.Sphere,new Vector3(.02f,-.17f-i*.07f,.165f),Vector3.one*.025f,ink);
        var neck=Bone(chest,"Neck",new Vector3(0f,.15f,0f));
        Shape(neck,"NeckSkin",PrimitiveType.Capsule,Vector3.zero,new Vector3(.11f,.075f,.11f),skin);
        var head=Bone(neck,"Head",new Vector3(0f,.11f,0f));
        Shape(head,"HeadShape",PrimitiveType.Sphere,new Vector3(0f,.045f,0f),new Vector3(.36f,.41f,.33f),skin);
        Shape(head,"HairCap",PrimitiveType.Sphere,new Vector3(0f,.19f,-.015f),new Vector3(.37f,.17f,.34f),hair);
        Shape(head,"HairPart",PrimitiveType.Cube,new Vector3(-.035f,.18f,.115f),new Vector3(.27f,.065f,.10f),hair,Quaternion.Euler(0f,0f,12f));
        foreach(int side in new[]{-1,1})
        {
            Shape(head,"Ear",PrimitiveType.Sphere,new Vector3(side*.18f,.045f,0f),new Vector3(.06f,.095f,.055f),skin);
            Shape(head,"EyeWhite",PrimitiveType.Sphere,new Vector3(side*.079f,.08f,.150f),new Vector3(.082f,.067f,.035f),white);
            Shape(head,"Pupil",PrimitiveType.Sphere,new Vector3(side*.075f,.078f,.174f),new Vector3(.031f,.041f,.015f),ink);
            Shape(head,"Eyebrow",PrimitiveType.Cube,new Vector3(side*.080f,.132f,.166f),new Vector3(.098f,.020f,.025f),hair,Quaternion.Euler(0f,0f,side*11f));
        }
        Shape(head,"Nose",PrimitiveType.Sphere,new Vector3(0f,.035f,.179f),new Vector3(.055f,.070f,.060f),skin);
        Shape(head,"Mouth",PrimitiveType.Cube,new Vector3(0f,-.049f,.160f),new Vector3(.105f,.013f,.018f),ink);

        foreach(string side in new[]{"L","R"})
        {
            float sign=side=="R"?1f:-1f;
            var upper=Bone(chest,"UpperArm"+side,new Vector3(sign*.20f,.11f,0f),Quaternion.Euler(90f,0f,0f));
            Limb(upper,"ShirtUpperArm"+side,Vector3.right*sign,.28f,.068f,shirt);
            var forearm=Bone(upper,"Forearm"+side,new Vector3(sign*.28f,0f,0f));
            Limb(forearm,"ShirtForearm"+side,Vector3.right*sign,.24f,.055f,shirt);
            var hand=Bone(forearm,"Hand"+side,new Vector3(sign*.24f,0f,0f),Quaternion.Euler(-90f,0f,0f));
            Shape(hand,"Mitten"+side,PrimitiveType.Sphere,new Vector3(sign*.045f,0f,0f),new Vector3(.13f,.073f,.078f),skin);
            var finger=Bone(hand,"Index"+side+"1",new Vector3(sign*.078f,0f,.017f));
            finger=Bone(finger,"Index"+side+"2",new Vector3(sign*.025f,0f,0f));
            Bone(finger,"Index"+side+"3",new Vector3(sign*.020f,0f,0f));
            var thigh=Bone(hips,"Thigh"+side,new Vector3(sign*.105f,-.02f,0f));
            Limb(thigh,"TrousersThigh"+side,Vector3.down,.40f,.088f,suit);
            var shin=Bone(thigh,"Shin"+side,new Vector3(0f,-.40f,0f));
            Limb(shin,"TrousersShin"+side,Vector3.down,.39f,.066f,suit);
            var foot=Bone(shin,"Foot"+side,new Vector3(0f,-.39f,0f));
            Shape(foot,"Shoe"+side,PrimitiveType.Cube,new Vector3(0f,-.045f,.065f),new Vector3(.17f,.105f,.29f),shoe);
        }
        return root;
    }
}
