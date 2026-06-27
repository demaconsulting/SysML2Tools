package OrderProcess {

    // An order-processing action flow with a branch and a join.
    action def ProcessOrder {
        action receiveOrder;
        action validateOrder;
        action checkStock;
        action pickItems;
        action backorder;
        action packOrder;
        action shipOrder;

        first receiveOrder then validateOrder;
        first validateOrder then checkStock;
        first checkStock then pickItems;
        first checkStock then backorder;
        first pickItems then packOrder;
        first backorder then packOrder;
        first packOrder then shipOrder;
    }

    // The view name contains "ActionFlow" to select the action-flow layout.
    view def OrderActionFlowView {}
}
