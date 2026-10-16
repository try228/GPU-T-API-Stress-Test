[[vk::binding(0, 0)]] RWStructuredBuffer<float> bufferA;
[[vk::binding(1, 0)]] RWStructuredBuffer<float> bufferB;
[[vk::binding(2, 0)]] RWStructuredBuffer<float> bufferC;

typealias MatA = linalg::CoopMat<float, MemoryScope.Workgroup, 16, 16, linalg::CoopMatMatrixUse.MatrixA>;
typealias MatB = linalg::CoopMat<float, MemoryScope.Workgroup, 16, 16, linalg::CoopMatMatrixUse.MatrixB>;
typealias MatC = linalg::CoopMat<float, MemoryScope.Workgroup, 16, 16, linalg::CoopMatMatrixUse.MatrixAccumulator>;

[ForceInline]
MatC coopMma(MatA a, MatB b, MatC c)
{
    return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
}

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID)
{
    uint outOffset = (groupId.x * 16 * 16) & 0x0001FFFFu;

    MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, 16);
    MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, 16);
    MatC acc;
    acc.fill(0.0f);

    [loop]
    for (int i = 0; i < 16; i++)
    {
        acc = coopMma(matA, matB, acc);
    }

    acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, 16);
}