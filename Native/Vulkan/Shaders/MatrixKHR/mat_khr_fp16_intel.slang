[[vk::binding(0, 0)]] RWStructuredBuffer<half> bufferA;
[[vk::binding(1, 0)]] RWStructuredBuffer<half> bufferB;
[[vk::binding(2, 0)]] RWStructuredBuffer<half> bufferC;

typealias MatA = linalg::CoopMat<half, MemoryScope.Subgroup, 8, 16, linalg::CoopMatMatrixUse.MatrixA>;
typealias MatB = linalg::CoopMat<half, MemoryScope.Subgroup, 16, 8, linalg::CoopMatMatrixUse.MatrixB>;
typealias MatC = linalg::CoopMat<half, MemoryScope.Subgroup, 8, 8, linalg::CoopMatMatrixUse.MatrixAccumulator>;

[ForceInline]
MatC coopMma(MatA a, MatB b, MatC c)
{
    return spirv_asm { result:$$MatC = OpCooperativeMatrixMulAddKHR $a $b $c; };
}

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint sgIdx = id.x / WaveGetLaneCount();
    uint outOffset = (sgIdx * 8 * 8) & 0x0001FFFFu;

    MatA matA = MatA.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferA, 0, 16);
    MatB matB = MatB.Load<linalg::CoopMatMatrixLayout.RowMajor>(bufferB, 0, 8);
    MatC acc;
    acc.fill(0.0h);

    [loop]
    for (int i = 0; i < 16; i++) acc = coopMma(matA, matB, acc);

    acc.Store<linalg::CoopMatMatrixLayout.RowMajor>(bufferC, outOffset, 8);
}