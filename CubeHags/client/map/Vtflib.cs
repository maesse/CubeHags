using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace CubeHags.client.map.Source
{

    public sealed class VtfLib
    {
        public const int iVersion = 127;
        public const string sVersion = "1.2.7";

        public const uint uiMajorVersion = 7;
        public const uint uiMinorVersion = 4;

        public enum Option
        {
            OptionDXTQuality = 0,

            OptionLuminanceWeightR,
            OptionLuminanceWeightG,
            OptionLuminanceWeightB,

            OptionBlueScreenMaskR,
            OptionBlueScreenMaskG,
            OptionBlueScreenMaskB,

            OptionBlueScreenClearR,
            OptionBlueScreenClearG,
            OptionBlueScreenClearB,

            OptionFP16HDRKey,
            OptionFP16HDRShift,
            OptionFP16HDRGamma,

            OptionUnsharpenRadius,
            OptionUnsharpenAmount,
            OptionUnsharpenThreshold,

            OptionXSharpenStrength,
            OptionXSharpenThreshold,

            OptionVMTParseMode
        }

        public enum ImageFormat
        {
            ImageFormatRGBA8888 = 0,
            ImageFormatABGR8888,
            ImageFormatRGB888,
            ImageFormatBGR888,
            ImageFormatRGB565,
            ImageFormatI8,
            ImageFormatIA88,
            ImageFormatP8,
            ImageFormatA8,
            ImageFormatRGB888BlueScreen,
            ImageFormatBGR888BlueScreen,
            ImageFormatARGB8888,
            ImageFormatBGRA8888,
            ImageFormatDXT1,
            ImageFormatDXT3,
            ImageFormatDXT5,
            ImageFormatBGRX8888,
            ImageFormatBGR565,
            ImageFormatBGRX5551,
            ImageFormatBGRA4444,
            ImageFormatDXT1OneBitAlpha,
            ImageFormatBGRA5551,
            ImageFormatUV88,
            ImageFormatUVWQ8888,
            ImageFormatRGBA16161616F,
            ImageFormatRGBA16161616,
            ImageFormatUVLX8888,
            ImageFormatI32F,
            ImageFormatRGB323232F,
            ImageFormatRGBA32323232F,
            ImageFormatCount,
            ImageFormatNone = -1
        }

        public enum ImageFlag : uint
        {
            ImageFlagNone = 0x00000000,
            ImageFlagPointSample = 0x00000001,
            ImageFlagTrilinear = 0x00000002,
            ImageFlagClampS = 0x00000004,
            ImageFlagClampT = 0x00000008,
            ImageFlagAnisotropic = 0x00000010,
            ImageFlagHintDXT5 = 0x00000020,
            ImageFlagSRGB = 0x00000040,
            ImageFlagNormal = 0x00000080,
            ImageFlagNoMIP = 0x00000100,
            ImageFlagNoLOD = 0x00000200,
            ImageFlagMinMIP = 0x00000400,
            ImageFlagProcedural = 0x00000800,
            ImageFlagOneBitAlpha = 0x00001000,
            ImageFlagEightBitAlpha = 0x00002000,
            ImageFlagEnviromentMap = 0x00004000,
            ImageFlagRenderTarget = 0x00008000,
            ImageFlagDepthRenderTarget = 0x00010000,
            ImageFlagNoDebugOverride = 0x00020000,
            ImageFlagSingleCopy = 0x00040000,
            ImageFlagUnused0 = 0x00080000,
            ImageFlagUnused1 = 0x00100000,
            ImageFlagUnused2 = 0x00200000,
            ImageFlagUnused3 = 0x00400000,
            ImageFlagNoDepthBuffer = 0x00800000,
            ImageFlagUnused4 = 0x01000000,
            ImageFlagClampU = 0x02000000,
            ImageFlagVertexTexture = 0x04000000,
            ImageFlagSSBump = 0x08000000,
            ImageFlagUnused5 = 0x10000000,
            ImageFlagBorder = 0x20000000,
            ImageFlagCount = 30
        }

        public enum CubemapFace
        {
            CubemapFaceRight = 0,
            CubemapFaceLeft,
            CubemapFaceBack,
            CubemapFaceFront,
            CubemapFaceUp,
            CubemapFaceDown,
            CubemapFaceSphereMap,
            CubemapFaceCount
        }

        public enum MipmapFilter
        {
            MipmapFilterPoint = 0,
            MipmapFilterBox,
            MipmapFilterTriangle,
            MipmapFilterQuadratic,
            MipmapFilterCubic,
            MipmapFilterCatrom,
            MipmapFilterMitchell,
            MipmapFilterGaussian,
            MipmapFilterSinC,
            MipmapFilterBessel,
            MipmapFilterHanning,
            MipmapFilterHamming,
            MipmapFilterBlackman,
            MipmapFilterKaiser,
            MipmapFilterCount
        }

        public enum SharpenFilter
        {
            SharpenFilterNone = 0,
            SharpenFilterNegative,
            SharpenFilterLighter,
            SharpenFilterDarker,
            SharpenFilterContrastMore,
            SharpenFilterContrastLess,
            SharpenFilterSmoothen,
            SharpenFilterSharpenSoft,
            SharpenFilterSharpenMeium,
            SharpenFilterSharpenStrong,
            SharpenFilterFindEdges,
            SharpenFilterContour,
            SharpenFilterEdgeDetect,
            SharpenFilterEdgeDetectSoft,
            SharpenFilterEmboss,
            SharpenFilterMeanRemoval,
            SharpenFilterUnsharp,
            SharpenFilterXSharpen,
            SharpenFilterWarpSharp,
            SharpenFilterCount
        }

        public enum DXTQuality
        {
            DXTQualityLow = 0,
            DXTQualityMedium,
            DXTQualityHigh,
            DXTQualityHighest,
            DXTQualityCount
        }

        public enum KernelFilter
        {
            KernelFilter4x = 0,
            KernelFilter3x3,
            KernelFilter5x5,
            KernelFilter7x7,
            KernelFilter9x9,
            KernelFilterDuDv,
            KernelFilterCount
        }

        public enum HeightConversionMethod
        {
            HeightConversionMethodAlpha = 0,
            HeightConversionMethodAverageRGB,
            HeightConversionMethodBiasedRGB,
            HeightConversionMethodRed,
            HeightConversionMethodGreed,
            HeightConversionMethodBlue,
            HeightConversionMethodMaxRGB,
            HeightConversionMethodColorSspace,
            //HeightConversionMethodNormalize,
            HeightConversionMethodCount
        }

        public enum NormalAlphaResult
        {
            NormalAlphaResultNoChange = 0,
            NormalAlphaResultHeight,
            NormalAlphaResultBlack,
            NormalAlphaResultWhite,
            NormalAlphaResultCount
        }

        public enum ResizeMethod
        {
            ResizeMethodNearestPowerTwo = 0,
            ResizeMethodBiggestPowerTwo,
            ResizeMethodSmallestPowerTwo,
            ResizeMethodSet,
            ResizeMethodCount
        }

        public enum ResourceFlag : uint
        {
            ResourceFlagNoDataChunk = 0x02,
            ResourceFlagCount = 1
        }

        public enum ResourceType : uint
        {
            ResourceTypeLowResolutionImage = 0x01,
            ResourceTypeImage = 0x30,
            ResourceTypeSheet = 0x10,
            ResourceTypeCRC = 'C' | ('R' << 8) | ('C' << 24) | (ResourceFlag.ResourceFlagNoDataChunk << 32),
            ResourceTypeLODControl = 'L' | ('O' << 8) | ('D' << 24) | (ResourceFlag.ResourceFlagNoDataChunk << 32),
            ResourceTypeTextureSettingsEx = 'T' | ('S' << 8) | ('O' << 24) | (ResourceFlag.ResourceFlagNoDataChunk << 32),
            ResourceTypeKeyValueData = 'K' | ('V' << 8) | ('D' << 24)
        }

        public const uint uiMaximumResources = 32;

        [StructLayout(LayoutKind.Explicit)]
        public struct ImageFormatInfo
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.LPStr)]
            public string sName;
            [FieldOffset(4)]
            public uint uiBitsPerPixel;
            [FieldOffset(8)]
            public uint uiBytesPerPixel;
            [FieldOffset(12)]
            public uint uiRedBitsPerPixel;
            [FieldOffset(16)]
            public uint uiGreenBitsPerPixel;
            [FieldOffset(20)]
            public uint uiBlueBitsPerPixel;
            [FieldOffset(24)]
            public uint uiAlphaBitsPerPixel;
            [FieldOffset(28)]
            public bool bIsCompressed;
            [FieldOffset(29)]
            public bool bIsSupported;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct CreateOptions
        {
            [FieldOffset(0)]
            public uint uiVersionMajor;
            [FieldOffset(4)]
            public uint uiVersionMinor;
            [FieldOffset(8)]
            public ImageFormat eImageFormat;

            [FieldOffset(12)]
            public uint uiFlags;
            [FieldOffset(16)]
            public uint uiStartFrame;
            [FieldOffset(20)]
            public float fBumpScale;
            [FieldOffset(24)]
            public float fRefectivityX;
            [FieldOffset(28)]
            public float fRefectivityY;
            [FieldOffset(32)]
            public float fRefectivityZ;

            [FieldOffset(36)]
            public bool bMipmaps;
            [FieldOffset(37)]
            public MipmapFilter eMipmapFilter;
            [FieldOffset(41)]
            public SharpenFilter eSharpenFilter;

            [FieldOffset(45)]
            public bool bThumbnail;
            [FieldOffset(46)]
            public bool bReflectivity;

            [FieldOffset(47)]
            public bool bResize;
            [FieldOffset(48)]
            public ResizeMethod eResizeMethod;
            [FieldOffset(52)]
            public MipmapFilter eResizeFilter;
            [FieldOffset(56)]
            public SharpenFilter eResizeSharpenFilter;
            [FieldOffset(60)]
            public uint uiResizeWidth;
            [FieldOffset(64)]
            public uint uiResizeHeight;

            [FieldOffset(68)]
            public bool bResizeClamp;
            [FieldOffset(69)]
            public uint uiResizeClampWidth;
            [FieldOffset(73)]
            public uint uiResizeClampHeight;

            [FieldOffset(77)]
            public bool bGammaCorrection;
            [FieldOffset(78)]
            public float fGammaCorrection;

            [FieldOffset(82)]
            public bool bNormalMap;
            [FieldOffset(83)]
            public KernelFilter eKernelFilter;
            [FieldOffset(87)]
            public HeightConversionMethod eHeightConversionMethod;
            [FieldOffset(91)]
            public NormalAlphaResult eNormalAlphaResult;
            [FieldOffset(95)]
            public byte uiNormalMinimumZ;
            [FieldOffset(96)]
            public float fNormalScale;
            [FieldOffset(100)]
            public bool bNormalWrap;
            [FieldOffset(101)]
            public bool bNormalInvertX;
            [FieldOffset(102)]
            public bool bNormalInvertY;
            [FieldOffset(103)]
            public bool bNormalInvertZ;

            [FieldOffset(104)]
            public bool bSphereMap;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct LODControlResource
        {
            [FieldOffset(0)]
            public byte uiResolutionClampU;
            [FieldOffset(1)]
            public byte uiResolutionClampV;
            [FieldOffset(2)]
            public byte uiPadding0;
            [FieldOffset(3)]
            public byte uiPadding1;
        }

        //
        // VTFLib
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlGetVersion();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern string vlGetVersionString();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern string vlGetLastError();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlInitialize();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlShutdown();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlGetBoolean(Option eOption);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlSetBoolean(Option eOption, bool bValue);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern int vlGetInteger(Option eOption);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlSetInteger(Option eOption, int iValue);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern float vlGetFloat(Option eOption);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlSetFloat(Option eOption, float fValue);

        //
        // Memory managment routines.
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageIsBound();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlBindImage(uint uiImage);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlCreateImage(uint* uiImage);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlDeleteImage(uint uiImage);

        //
        // Library routines.  (Basically class wrappers.)
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageCreateDefaultCreateStructure(out CreateOptions CreateOptions);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageCreate(uint uiWidth, uint uiHeight, uint uiFrames, uint uiFaces, uint uiSlices, ImageFormat ImageFormat, bool bThumbnail, bool bMipmaps, bool bNullImageData);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageCreateSingle(uint uiWidth, uint uiHeight, byte* lpImageDataRGBA8888, ref CreateOptions CreateOptions);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageCreateMultiple(uint uiWidth, uint uiHeight, uint uiFrames, uint uiFaces, uint uiSlices, byte** lpImageDataRGBA8888, ref CreateOptions CreateOptions);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageDestroy();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageIsLoaded();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageLoad(string sFileName, bool bHeaderOnly);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageLoadLump(void* lpData, uint uiBufferSize, bool bHeaderOnly);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageSave(string sFileName);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageSaveLump(void* lpData, uint uiBufferSize, uint* uiSize);

        //
        // Image routines.
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetHasImage();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetMajorVersion();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetMinorVersion();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetSize();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetWidth();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetHeight();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetDepth();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetFrameCount();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetFaceCount();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetMipmapCount();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetStartFrame();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetStartFrame(uint uiStartFrame);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetFlags();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetFlags(uint uiFlags);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGetFlag(ImageFlag ImageFlag);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetFlag(ImageFlag ImageFlag, bool bState);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern float vlImageGetBumpmapScale();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetBumpmapScale(float fBumpmapScale);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageGetReflectivity(float* fRed, float* fGreen, float* fBlue);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetReflectivity(float fRed, float fGreen, float fBlue);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern ImageFormat vlImageGetFormat();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern byte* vlImageGetData(uint uiFrame, uint uiFace, uint uiSlice, uint uiMipmapLevel);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetData(uint uiFrame, uint uiFace, uint uiSlice, uint uiMipmapLevel, byte* lpData);

        //
        // Thumbnail routines.
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGetHasThumbnail();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetThumbnailWidth();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetThumbnailHeight();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern ImageFormat vlImageGetThumbnailFormat();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern byte* vlImageGetThumbnailData();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageSetThumbnailData(byte* lpData);

        //
        // Resource routines.
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGetSupportsResources();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetResourceCount();
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageGetResourceType(uint uiIndex);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGetHasResource(ResourceType ResourceType);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void* vlImageGetResourceData(ResourceType ResourceType, uint* uiSize);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void* vlImageSetResourceData(ResourceType ResourceType, uint uiSize, void* lpData);

        //
        // Helper routines.
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGenerateMipmaps(uint uiFace, uint uiFrame, MipmapFilter MipmapFilter, SharpenFilter SharpenFilter);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGenerateAllMipmaps(MipmapFilter MipmapFilter, SharpenFilter SharpenFilter);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGenerateThumbnail();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGenerateNormalMap(uint uiFrame, KernelFilter KernelFilter, HeightConversionMethod HeightConversionMethod, NormalAlphaResult NormalAlphaResult);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGenerateAllNormalMaps(KernelFilter KernelFilter, HeightConversionMethod HeightConversionMethod, NormalAlphaResult NormalAlphaResult);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGenerateSphereMap();

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageComputeReflectivity();

        //
        // Conversion routines.
        //

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageGetImageFormatInfoEx(ImageFormat ImageFormat, out ImageFormatInfo ImageFormatInfo);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageComputeImageSize(uint uiWidth, uint uiHeight, uint uiDepth, uint uiMipmaps, ImageFormat ImageFormat);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageComputeMipmapCount(uint uiWidth, uint uiHeight, uint uiDepth);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageComputeMipmapDimensions(uint uiWidth, uint uiHeight, uint uiDepth, uint uiMipmapLevel, uint* uiMipmapWidth, uint* uiMipmapHeight, uint* uiMipmapDepth);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern uint vlImageComputeMipmapSize(uint uiWidth, uint uiHeight, uint uiDepth, uint uiMipmapLevel, ImageFormat ImageFormat);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageConvert(byte* lpSource, byte* lpDest, uint uiWidth, uint uiHeight, ImageFormat SourceFormat, ImageFormat DestFormat);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageConvertToNormalMap(byte* lpSourceRGBA8888, byte* lpDestRGBA8888, uint uiWidth, uint uiHeight, KernelFilter KernelFilter, HeightConversionMethod HeightConversionMethod, NormalAlphaResult NormalAlphaResult, byte bMinimumZ, float fScale, bool bWrap, bool bInvertX, bool bInvertY);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern bool vlImageResize(byte* lpSourceRGBA8888, byte* lpDestRGBA8888, uint uiSourceWidth, uint uiSourceHeight, uint uiDestWidth, uint uiDestHeight, MipmapFilter ResizeFilter, SharpenFilter SharpenFilter);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageCorrectImageGamma(byte* lpImageDataRGBA8888, uint uiWidth, uint uiHeight, float fGammaCorrection);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageComputeImageReflectivity(byte* lpImageDataRGBA8888, uint uiWidth, uint uiHeight, float* sX, float* sY, float* sZ);

        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageFlipImage(byte* lpImageDataRGBA8888, uint uiWidth, uint uiHeight);
        [DllImport("VTFLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public unsafe static extern void vlImageMirrorImage(byte* lpImageDataRGBA8888, uint uiWidth, uint uiHeight);
    }
}
